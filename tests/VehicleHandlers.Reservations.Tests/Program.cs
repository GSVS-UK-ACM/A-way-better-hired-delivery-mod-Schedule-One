using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VehicleHandlers.Contracts;
using VehicleHandlers.Reservations;

namespace VehicleHandlers.Reservations.Tests
{
    internal static class Program
    {
        private static int assertionCount;

        private static int Main()
        {
            try
            {
                VerifyAcquisitionAndOwnership();
                VerifyExactLeaseRelease();
                VerifyHandlerCleanupAndSnapshots();
                VerifyConcurrentAcquisition();

                Console.WriteLine(
                    $"Vehicle Handlers reservation verification passed ({assertionCount} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Reservation verification failed: {exception.Message}");
                return 1;
            }
        }

        private static void VerifyAcquisitionAndOwnership()
        {
            HandlerReservationRegistry registry = new HandlerReservationRegistry();
            LoadingBayKey bay = Bay(1);
            ReservationOwner owner = Owner(1, 1);

            ReservationAcquireResult invalidBay = registry.TryAcquire(default, owner);
            Assert(invalidBay.Status == ReservationAcquireStatus.InvalidRequest &&
                invalidBay.Lease.IsEmpty,
                "An empty bay must be rejected without a lease.");

            ReservationAcquireResult invalidOwner = registry.TryAcquire(bay, default);
            Assert(invalidOwner.Status == ReservationAcquireStatus.InvalidRequest &&
                invalidOwner.Lease.IsEmpty,
                "An empty owner must be rejected without a lease.");

            ReservationAcquireResult acquired = registry.TryAcquire(bay, owner);
            Assert(acquired.Status == ReservationAcquireStatus.Acquired && acquired.Acquired,
                "A free bay should be acquired.");
            Assert(!acquired.Lease.IsEmpty && acquired.Lease.Token == 1,
                "The first acquisition should return the first positive token.");
            Assert(registry.IsReserved(bay),
                "An acquired bay should report reserved.");
            Assert(registry.IsOwnedBy(bay, owner),
                "The acquiring Handler trip should own the bay.");
            Assert(!registry.IsOwnedBy(bay, Owner(2, 2)),
                "A different Handler trip must not own the bay.");

            ReservationAcquireResult repeated = registry.TryAcquire(bay, owner);
            Assert(repeated.Status == ReservationAcquireStatus.AlreadyOwned && repeated.Acquired,
                "Acquisition should be idempotent for the same owner.");
            Assert(repeated.Lease.Equals(acquired.Lease),
                "Idempotent acquisition should return the exact existing lease.");

            ReservationAcquireResult blocked = registry.TryAcquire(bay, Owner(2, 2));
            Assert(blocked.Status == ReservationAcquireStatus.OwnedByAnother &&
                !blocked.Acquired && blocked.Lease.IsEmpty,
                "A foreign owner must not displace an existing lease.");
            Assert(!registry.IsReserved(default) && !registry.IsOwnedBy(default, owner),
                "Empty keys must never appear reserved or owned.");
        }

        private static void VerifyExactLeaseRelease()
        {
            HandlerReservationRegistry registry = new HandlerReservationRegistry();
            LoadingBayKey bay = Bay(10);
            ReservationOwner owner = Owner(10, 10);
            ReservationLease lease = registry.TryAcquire(bay, owner).Lease;

            ReservationLease foreign = new ReservationLease(bay, Owner(11, 11), lease.Token);
            ReservationReleaseResult wrongOwner = registry.Release(foreign);
            Assert(wrongOwner.Status == ReservationReleaseStatus.NotOwner &&
                registry.IsOwnedBy(bay, owner),
                "A foreign owner must not release the lease.");

            ReservationLease stale = new ReservationLease(bay, owner, lease.Token + 1);
            ReservationReleaseResult staleResult = registry.Release(stale);
            Assert(staleResult.Status == ReservationReleaseStatus.StaleLease &&
                registry.IsReserved(bay),
                "A stale token must not release the lease.");

            ReservationReleaseResult released = registry.Release(lease);
            Assert(released.Status == ReservationReleaseStatus.Released && released.Released,
                "The exact lease should release successfully.");
            Assert(!registry.IsReserved(bay),
                "A released bay should become available.");
            Assert(registry.Release(lease).Status == ReservationReleaseStatus.NotFound,
                "Repeating release should report that the lease is gone.");
            Assert(registry.Release(default).Status == ReservationReleaseStatus.InvalidRequest,
                "An empty lease must be rejected.");

            ReservationLease replacement = registry.TryAcquire(bay, owner).Lease;
            Assert(replacement.Token > lease.Token,
                "A reacquired bay must receive a later token.");
            Assert(registry.Release(lease).Status == ReservationReleaseStatus.StaleLease,
                "An old lease must not release a later acquisition.");
            Assert(registry.IsOwnedBy(bay, owner),
                "The replacement lease must survive stale release attempts.");
        }

        private static void VerifyHandlerCleanupAndSnapshots()
        {
            HandlerReservationRegistry registry = new HandlerReservationRegistry();
            HandlerId firstHandler = Handler(20);
            HandlerId secondHandler = Handler(21);
            registry.TryAcquire(Bay(22), new ReservationOwner(firstHandler, Trip(22)));
            registry.TryAcquire(Bay(20), new ReservationOwner(firstHandler, Trip(20)));
            registry.TryAcquire(Bay(21), new ReservationOwner(secondHandler, Trip(21)));

            IReadOnlyList<ReservationLease> beforeCleanup = registry.Snapshot();
            Assert(beforeCleanup.Count == 3,
                "Snapshot should contain every active lease.");
            Assert(beforeCleanup[0].Bay == Bay(20) &&
                beforeCleanup[1].Bay == Bay(21) &&
                beforeCleanup[2].Bay == Bay(22),
                "Snapshot ordering should be deterministic by property, index, and dock.");

            int released = registry.ReleaseForHandler(firstHandler);
            Assert(released == 2,
                "Lifecycle cleanup should release every lease for one Handler.");
            Assert(!registry.IsReserved(Bay(20)) && !registry.IsReserved(Bay(22)),
                "Lifecycle cleanup should remove all of the Handler's bays.");
            Assert(registry.IsOwnedBy(Bay(21), new ReservationOwner(secondHandler, Trip(21))),
                "Lifecycle cleanup must preserve another Handler's lease.");
            Assert(registry.ReleaseForHandler(firstHandler) == 0,
                "Repeated lifecycle cleanup should be idempotent.");
            Assert(registry.ReleaseForHandler(default) == 0,
                "An empty Handler identity should release nothing.");
            Assert(beforeCleanup.Count == 3,
                "Previously returned snapshots must remain immutable after cleanup.");
            Assert(registry.Snapshot().Count == 1,
                "The current snapshot should reflect cleanup.");
        }

        private static void VerifyConcurrentAcquisition()
        {
            HandlerReservationRegistry registry = new HandlerReservationRegistry();
            LoadingBayKey bay = Bay(30);
            ReservationOwner first = Owner(30, 30);
            ReservationOwner second = Owner(31, 31);
            ReservationAcquireResult[] results = new ReservationAcquireResult[16];
            Task[] tasks = new Task[results.Length];

            for (int index = 0; index < tasks.Length; index++)
            {
                int captured = index;
                ReservationOwner contender = index % 2 == 0 ? first : second;
                tasks[index] = Task.Run(() =>
                    results[captured] = registry.TryAcquire(bay, contender));
            }

            Task.WaitAll(tasks);

            int newlyAcquired = 0;
            int alreadyOwned = 0;
            int blocked = 0;
            for (int index = 0; index < results.Length; index++)
            {
                switch (results[index].Status)
                {
                    case ReservationAcquireStatus.Acquired:
                        newlyAcquired++;
                        break;
                    case ReservationAcquireStatus.AlreadyOwned:
                        alreadyOwned++;
                        break;
                    case ReservationAcquireStatus.OwnedByAnother:
                        blocked++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected concurrent result: {results[index].Status}.");
                }
            }

            Assert(newlyAcquired == 1,
                "Exactly one concurrent caller should create the lease.");
            Assert(alreadyOwned == 7 && blocked == 8,
                "The winning owner should receive idempotent results and the other owner should be blocked.");
            Assert(registry.Snapshot().Count == 1,
                "Concurrent acquisition must create exactly one lease.");
            ReservationLease winner = registry.Snapshot()[0];
            Assert(winner.Bay == bay && (winner.Owner == first || winner.Owner == second),
                "The single lease should belong to one complete contender identity.");
        }

        private static HandlerId Handler(int value) =>
            new HandlerId(new Guid(value, 0, 0, new byte[8]));

        private static TripId Trip(int value) =>
            new TripId(new Guid(1000 + value, 0, 0, new byte[8]));

        private static ReservationOwner Owner(int handler, int trip) =>
            new ReservationOwner(Handler(handler), Trip(trip));

        private static LoadingBayKey Bay(int value) =>
            new LoadingBayKey(
                new PropertyKey($"PROPERTY-{value:D2}"),
                new DockId(new Guid(2000 + value, 0, 0, new byte[8])),
                value);

        private static void Assert(bool condition, string message)
        {
            assertionCount++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
