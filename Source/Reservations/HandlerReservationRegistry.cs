using System;
using System.Collections.Generic;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Reservations
{
    internal sealed class HandlerReservationRegistry : IHandlerReservationRegistry
    {
        private readonly object sync = new object();
        private readonly Dictionary<LoadingBayKey, ReservationLease> leases =
            new Dictionary<LoadingBayKey, ReservationLease>();
        private long nextToken = 1;

        public ReservationAcquireResult TryAcquire(LoadingBayKey bay, ReservationOwner owner)
        {
            if (bay.IsEmpty || owner.IsEmpty)
            {
                return new ReservationAcquireResult(
                    ReservationAcquireStatus.InvalidRequest,
                    default,
                    "A loading bay and Handler trip owner are required.");
            }

            lock (sync)
            {
                if (leases.TryGetValue(bay, out ReservationLease existing))
                {
                    if (existing.Owner == owner)
                    {
                        return new ReservationAcquireResult(
                            ReservationAcquireStatus.AlreadyOwned,
                            existing,
                            "The Handler trip already owns this loading bay.");
                    }

                    return new ReservationAcquireResult(
                        ReservationAcquireStatus.OwnedByAnother,
                        default,
                        "The loading bay is reserved by another Handler trip.");
                }

                if (nextToken == long.MaxValue)
                {
                    throw new InvalidOperationException("The reservation token space is exhausted.");
                }

                ReservationLease lease = new ReservationLease(bay, owner, nextToken++);
                leases.Add(bay, lease);
                return new ReservationAcquireResult(
                    ReservationAcquireStatus.Acquired,
                    lease,
                    "The loading bay reservation was acquired.");
            }
        }

        public bool IsReserved(LoadingBayKey bay)
        {
            if (bay.IsEmpty)
            {
                return false;
            }

            lock (sync)
            {
                return leases.ContainsKey(bay);
            }
        }

        public bool IsOwnedBy(LoadingBayKey bay, ReservationOwner owner)
        {
            if (bay.IsEmpty || owner.IsEmpty)
            {
                return false;
            }

            lock (sync)
            {
                return leases.TryGetValue(bay, out ReservationLease lease) &&
                    lease.Owner == owner;
            }
        }

        public ReservationReleaseResult Release(ReservationLease lease)
        {
            if (lease.IsEmpty)
            {
                return new ReservationReleaseResult(
                    ReservationReleaseStatus.InvalidRequest,
                    "A complete reservation lease is required.");
            }

            lock (sync)
            {
                if (!leases.TryGetValue(lease.Bay, out ReservationLease current))
                {
                    return new ReservationReleaseResult(
                        ReservationReleaseStatus.NotFound,
                        "The loading bay has no Handler reservation.");
                }

                if (current.Owner != lease.Owner)
                {
                    return new ReservationReleaseResult(
                        ReservationReleaseStatus.NotOwner,
                        "The lease is owned by another Handler trip.");
                }

                if (current.Token != lease.Token)
                {
                    return new ReservationReleaseResult(
                        ReservationReleaseStatus.StaleLease,
                        "The lease token is stale.");
                }

                leases.Remove(lease.Bay);
                return new ReservationReleaseResult(
                    ReservationReleaseStatus.Released,
                    "The loading bay reservation was released.");
            }
        }

        public int ReleaseForHandler(HandlerId handler)
        {
            if (handler.IsEmpty)
            {
                return 0;
            }

            lock (sync)
            {
                List<LoadingBayKey> ownedBays = new List<LoadingBayKey>();
                foreach (KeyValuePair<LoadingBayKey, ReservationLease> pair in leases)
                {
                    if (pair.Value.Owner.Handler == handler)
                    {
                        ownedBays.Add(pair.Key);
                    }
                }

                for (int index = 0; index < ownedBays.Count; index++)
                {
                    leases.Remove(ownedBays[index]);
                }

                return ownedBays.Count;
            }
        }

        public IReadOnlyList<ReservationLease> Snapshot()
        {
            lock (sync)
            {
                List<ReservationLease> snapshot = new List<ReservationLease>(leases.Values);
                snapshot.Sort(CompareLeases);
                return snapshot;
            }
        }

        private static int CompareLeases(ReservationLease left, ReservationLease right)
        {
            int property = StringComparer.OrdinalIgnoreCase.Compare(
                left.Bay.Property.PropertyCode,
                right.Bay.Property.PropertyCode);
            if (property != 0)
            {
                return property;
            }

            int dockIndex = left.Bay.DockIndex.CompareTo(right.Bay.DockIndex);
            if (dockIndex != 0)
            {
                return dockIndex;
            }

            return string.CompareOrdinal(left.Bay.Dock.ToString(), right.Bay.Dock.ToString());
        }
    }
}
