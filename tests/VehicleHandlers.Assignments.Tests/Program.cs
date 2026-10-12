using System;
using System.Collections.Generic;
using VehicleHandlers.Assignments;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Assignments.Tests
{
    internal static class Program
    {
        private static int assertionCount;

        private static int Main()
        {
            try
            {
                VerifyConfigurationAndUniqueness();
                VerifyTripTransitions();
                VerifyRemovalAndRevisionTombstones();
                VerifyRestoreBoundary();

                Console.WriteLine($"Vehicle Handlers assignment verification passed ({assertionCount} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Assignment verification failed: {exception.Message}");
                return 1;
            }
        }

        private static void VerifyConfigurationAndUniqueness()
        {
            HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
            HandlerId firstHandler = Handler(1);
            HandlerId secondHandler = Handler(2);
            VehicleId firstVehicle = Vehicle(1);
            VehicleId secondVehicle = Vehicle(2);

            AssignmentWriteResult created = registry.TryConfigure(
                Configuration(firstHandler, firstVehicle, Bay(1), true, false),
                0);
            AssertWrite(created, AssignmentWriteStatus.Applied, 1,
                "A new assignment should advance the initial revision.");
            Assert(registry.TryGet(firstHandler, out HandlerAssignmentSnapshot first),
                "A configured assignment should be readable.");
            Assert(first.Vehicle == firstVehicle && first.TripState == HandlerTripState.Idle,
                "Configuration should create the requested idle assignment.");
            Assert(first.Enabled && !first.ManuallyHidden,
                "Configuration flags should be preserved.");

            IReadOnlyList<HandlerAssignmentSnapshot> beforeReconfigure = registry.Snapshot();
            Assert(beforeReconfigure.Count == 1,
                "Snapshot should include the configured Handler exactly once.");

            AssignmentWriteResult stale = registry.TryConfigure(
                Configuration(firstHandler, secondVehicle, Bay(2), true, false),
                0);
            AssertWrite(stale, AssignmentWriteStatus.StaleRevision, 1,
                "Stale configuration should report the authoritative revision.");

            AssignmentWriteResult duplicate = registry.TryConfigure(
                Configuration(secondHandler, firstVehicle, Bay(2), true, false),
                0);
            AssertWrite(duplicate, AssignmentWriteStatus.DuplicateVehicle, 0,
                "A vehicle cannot be assigned to two Handlers.");

            AssignmentWriteResult replaced = registry.TryConfigure(
                Configuration(firstHandler, secondVehicle, Bay(3), false, true),
                1);
            AssertWrite(replaced, AssignmentWriteStatus.Applied, 2,
                "Reconfiguration should advance the Handler revision.");
            Assert(registry.TryGet(firstHandler, out first),
                "The reconfigured Handler should remain readable.");
            Assert(first.Vehicle == secondVehicle && first.Destination == Bay(3),
                "Reconfiguration should atomically replace vehicle and destination.");
            Assert(!first.Enabled && first.ManuallyHidden,
                "Reconfiguration should atomically replace control flags.");
            Assert(beforeReconfigure.Count == 1 && beforeReconfigure[0].Vehicle == firstVehicle,
                "A previously returned snapshot should remain immutable after registry changes.");

            AssignmentWriteResult reusedVehicle = registry.TryConfigure(
                Configuration(secondHandler, firstVehicle, Bay(2), true, false),
                0);
            AssertWrite(reusedVehicle, AssignmentWriteStatus.Applied, 1,
                "Replacing a Handler vehicle should release the old vehicle index.");

            IReadOnlyList<HandlerAssignmentSnapshot> ordered = registry.Snapshot();
            Assert(ordered.Count == 2,
                "Snapshot should return one assignment per Handler.");
            Assert(string.CompareOrdinal(
                ordered[0].Handler.ToString(),
                ordered[1].Handler.ToString()) < 0,
                "Snapshot order should be deterministic by Handler identity.");

            AssertThrows<ArgumentNullException>(() => registry.TryConfigure(null, 0),
                "Null configuration should be rejected.");
            AssertThrows<ArgumentOutOfRangeException>(() => registry.TryConfigure(
                Configuration(Handler(3), Vehicle(3), Bay(3), true, false),
                -1), "Negative expected revisions should be rejected.");
        }

        private static void VerifyTripTransitions()
        {
            HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
            HandlerId handler = Handler(10);
            registry.TryConfigure(Configuration(handler, Vehicle(10), Bay(10), true, false), 0);

            TripTransitionResult missingTrip = registry.TryTransition(
                handler,
                default,
                HandlerTripState.Reserved,
                1);
            AssertTransition(missingTrip, TripTransitionStatus.IllegalTransition,
                HandlerTripState.Idle, HandlerTripState.Idle, 1,
                "Starting a trip without an identity should fail.");

            TripId trip = Trip(10);
            TripTransitionResult reserved = registry.TryTransition(
                handler,
                trip,
                HandlerTripState.Reserved,
                1);
            AssertTransition(reserved, TripTransitionStatus.Applied,
                HandlerTripState.Idle, HandlerTripState.Reserved, 2,
                "Idle to Reserved should bind the supplied trip.");
            Assert(registry.TryGet(handler, out HandlerAssignmentSnapshot active) && active.ActiveTrip == trip,
                "The first active transition should store the exact trip identity.");

            TripTransitionResult noChange = registry.TryTransition(
                handler,
                trip,
                HandlerTripState.Reserved,
                2);
            AssertTransition(noChange, TripTransitionStatus.NoChange,
                HandlerTripState.Reserved, HandlerTripState.Reserved, 2,
                "Repeating an owned transition should be idempotent.");

            TripTransitionResult foreignTrip = registry.TryTransition(
                handler,
                Trip(11),
                HandlerTripState.InTransit,
                2);
            AssertTransition(foreignTrip, TripTransitionStatus.IllegalTransition,
                HandlerTripState.Reserved, HandlerTripState.Reserved, 2,
                "A foreign trip must not advance an assignment.");

            TripTransitionResult stale = registry.TryTransition(
                handler,
                trip,
                HandlerTripState.InTransit,
                1);
            AssertTransition(stale, TripTransitionStatus.StaleRevision,
                HandlerTripState.Reserved, HandlerTripState.Reserved, 2,
                "A stale trip transition should report the authoritative state.");

            long revision = 2;
            revision = ApplyTransition(registry, handler, trip,
                HandlerTripState.Reserved, HandlerTripState.InTransit, revision);
            revision = ApplyTransition(registry, handler, trip,
                HandlerTripState.InTransit, HandlerTripState.Placing, revision);
            revision = ApplyTransition(registry, handler, trip,
                HandlerTripState.Placing, HandlerTripState.Placed, revision);
            revision = ApplyTransition(registry, handler, trip,
                HandlerTripState.Placed, HandlerTripState.Releasing, revision);
            revision = ApplyTransition(registry, handler, trip,
                HandlerTripState.Releasing, HandlerTripState.Idle, revision);

            Assert(registry.TryGet(handler, out HandlerAssignmentSnapshot idle),
                "The assignment should remain after a completed trip.");
            Assert(idle.TripState == HandlerTripState.Idle && idle.ActiveTrip.IsEmpty,
                "Returning to Idle should clear the active trip identity.");

            TripTransitionResult illegalJump = registry.TryTransition(
                handler,
                Trip(12),
                HandlerTripState.Placing,
                revision);
            AssertTransition(illegalJump, TripTransitionStatus.IllegalTransition,
                HandlerTripState.Idle, HandlerTripState.Idle, revision,
                "Illegal state jumps should leave the assignment unchanged.");

            TripTransitionResult missingAssignment = registry.TryTransition(
                Handler(99),
                Trip(99),
                HandlerTripState.Reserved,
                0);
            AssertTransition(missingAssignment, TripTransitionStatus.IllegalTransition,
                HandlerTripState.Idle, HandlerTripState.Idle, 0,
                "Transitions require an existing assignment.");

            AssertThrows<ArgumentOutOfRangeException>(() => registry.TryTransition(
                handler,
                trip,
                HandlerTripState.Reserved,
                -1), "Negative transition revisions should be rejected.");
        }

        private static void VerifyRemovalAndRevisionTombstones()
        {
            HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
            HandlerId handler = Handler(20);
            VehicleId vehicle = Vehicle(20);
            registry.TryConfigure(Configuration(handler, vehicle, Bay(20), true, false), 0);

            AssignmentWriteResult staleRemoval = registry.TryRemove(handler, 0);
            AssertWrite(staleRemoval, AssignmentWriteStatus.StaleRevision, 1,
                "Stale removal should report the current revision.");

            TripId trip = Trip(20);
            registry.TryTransition(handler, trip, HandlerTripState.Reserved, 1);
            AssignmentWriteResult activeRemoval = registry.TryRemove(handler, 2);
            AssertWrite(activeRemoval, AssignmentWriteStatus.ActiveTrip, 2,
                "An active assignment cannot be removed.");
            registry.TryTransition(handler, trip, HandlerTripState.Idle, 2);

            AssignmentWriteResult removed = registry.TryRemove(handler, 3);
            AssertWrite(removed, AssignmentWriteStatus.Applied, 4,
                "Removal should advance and retain a tombstone revision.");
            Assert(!registry.TryGet(handler, out _),
                "A removed assignment should no longer be visible.");
            Assert(registry.Snapshot().Count == 0,
                "Removed assignments should not appear in snapshots.");

            AssignmentWriteResult staleRecreate = registry.TryConfigure(
                Configuration(handler, vehicle, Bay(20), true, false),
                0);
            AssertWrite(staleRecreate, AssignmentWriteStatus.StaleRevision, 4,
                "Removal tombstones should prevent stale recreation.");

            AssignmentWriteResult recreated = registry.TryConfigure(
                Configuration(handler, vehicle, Bay(20), true, false),
                4);
            AssertWrite(recreated, AssignmentWriteStatus.Applied, 5,
                "A caller holding the removal revision may recreate the assignment.");

            AssignmentWriteResult missing = registry.TryRemove(Handler(21), 0);
            AssertWrite(missing, AssignmentWriteStatus.NotFound, 0,
                "Removing an unknown Handler should report NotFound.");
            AssertThrows<ArgumentOutOfRangeException>(() => registry.TryRemove(handler, -1),
                "Negative removal revisions should be rejected.");
        }

        private static void VerifyRestoreBoundary()
        {
            HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
            HandlerAssignmentSnapshot first = Snapshot(
                Handler(30),
                Vehicle(30),
                Bay(30),
                Trip(30),
                HandlerTripState.InTransit,
                7);
            AssignmentWriteResult restored = registry.TryRestore(first);
            AssertWrite(restored, AssignmentWriteStatus.Applied, 7,
                "Restore should preserve the saved revision.");
            Assert(registry.TryGet(first.Handler, out HandlerAssignmentSnapshot loaded) &&
                loaded.ActiveTrip == first.ActiveTrip && loaded.TripState == first.TripState,
                "Restore should preserve recoverable trip identity and state.");

            HandlerAssignmentSnapshot duplicateHandler = Snapshot(
                first.Handler,
                Vehicle(31),
                Bay(31),
                default,
                HandlerTripState.Idle,
                2);
            AssignmentWriteResult handlerConflict = registry.TryRestore(duplicateHandler);
            AssertWrite(handlerConflict, AssignmentWriteStatus.RestoreConflict, 7,
                "Restore should reject duplicate Handler identities.");

            HandlerAssignmentSnapshot duplicateVehicle = Snapshot(
                Handler(31),
                first.Vehicle,
                Bay(31),
                default,
                HandlerTripState.Idle,
                2);
            AssignmentWriteResult vehicleConflict = registry.TryRestore(duplicateVehicle);
            AssertWrite(vehicleConflict, AssignmentWriteStatus.RestoreConflict, 2,
                "Restore should reject duplicate vehicle identities.");

            HandlerAssignmentSnapshot second = Snapshot(
                Handler(32),
                Vehicle(32),
                Bay(32),
                default,
                HandlerTripState.Idle,
                4);
            AssertWrite(registry.TryRestore(second), AssignmentWriteStatus.Applied, 4,
                "Multiple distinct saved assignments may be restored in one load phase.");

            TripTransitionResult resumed = registry.TryTransition(
                first.Handler,
                first.ActiveTrip,
                HandlerTripState.Placing,
                7);
            AssertTransition(resumed, TripTransitionStatus.Applied,
                HandlerTripState.InTransit, HandlerTripState.Placing, 8,
                "A restored active assignment should resume through legal transitions.");

            HandlerAssignmentSnapshot late = Snapshot(
                Handler(33),
                Vehicle(33),
                Bay(33),
                default,
                HandlerTripState.Idle,
                1);
            AssertWrite(registry.TryRestore(late), AssignmentWriteStatus.RestoreConflict, 1,
                "Restore should close when normal mutations begin.");

            HandlerAssignmentRegistry invalidRegistry = new HandlerAssignmentRegistry();
            HandlerAssignmentSnapshot invalid = Snapshot(
                Handler(40),
                Vehicle(40),
                Bay(40),
                Trip(40),
                (HandlerTripState)99,
                1);
            AssertWrite(invalidRegistry.TryRestore(invalid),
                AssignmentWriteStatus.RejectedByValidation,
                1,
                "Unknown saved trip states should be rejected.");
            AssertThrows<ArgumentNullException>(() => invalidRegistry.TryRestore(null),
                "Null saved assignments should be rejected.");
        }

        private static long ApplyTransition(
            HandlerAssignmentRegistry registry,
            HandlerId handler,
            TripId trip,
            HandlerTripState previous,
            HandlerTripState target,
            long revision)
        {
            TripTransitionResult result = registry.TryTransition(handler, trip, target, revision);
            AssertTransition(result, TripTransitionStatus.Applied,
                previous, target, revision + 1,
                $"The transition {previous} -> {target} should be applied.");
            return result.Revision;
        }

        private static HandlerAssignmentConfiguration Configuration(
            HandlerId handler,
            VehicleId vehicle,
            LoadingBayKey bay,
            bool enabled,
            bool manuallyHidden) =>
            new HandlerAssignmentConfiguration(handler, vehicle, bay, enabled, manuallyHidden);

        private static HandlerAssignmentSnapshot Snapshot(
            HandlerId handler,
            VehicleId vehicle,
            LoadingBayKey bay,
            TripId trip,
            HandlerTripState state,
            long revision) =>
            new HandlerAssignmentSnapshot(
                handler,
                vehicle,
                bay,
                true,
                false,
                trip,
                state,
                revision);

        private static HandlerId Handler(int value) =>
            new HandlerId(new Guid(value, 0, 0, new byte[8]));

        private static VehicleId Vehicle(int value) =>
            new VehicleId(new Guid(1000 + value, 0, 0, new byte[8]));

        private static TripId Trip(int value) =>
            new TripId(new Guid(2000 + value, 0, 0, new byte[8]));

        private static LoadingBayKey Bay(int value) =>
            new LoadingBayKey(
                new PropertyKey($"PROPERTY-{value}"),
                new DockId(new Guid(3000 + value, 0, 0, new byte[8])),
                value);

        private static void AssertWrite(
            AssignmentWriteResult result,
            AssignmentWriteStatus expectedStatus,
            long expectedRevision,
            string message)
        {
            Assert(result.Status == expectedStatus && result.Revision == expectedRevision,
                $"{message} Got {result.Status} at revision {result.Revision}.");
        }

        private static void AssertTransition(
            TripTransitionResult result,
            TripTransitionStatus expectedStatus,
            HandlerTripState expectedPrevious,
            HandlerTripState expectedCurrent,
            long expectedRevision,
            string message)
        {
            Assert(result.Status == expectedStatus &&
                result.PreviousState == expectedPrevious &&
                result.CurrentState == expectedCurrent &&
                result.Revision == expectedRevision,
                $"{message} Got {result.Status}, {result.PreviousState} -> {result.CurrentState}, revision {result.Revision}.");
        }

        private static void Assert(bool condition, string message)
        {
            assertionCount++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertThrows<TException>(Action action, string message)
            where TException : Exception
        {
            assertionCount++;
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
