using System;
using System.Collections.Generic;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Assignments
{
    internal sealed class HandlerAssignmentRegistry : IHandlerAssignmentRegistry
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<HandlerId, HandlerAssignmentSnapshot> assignments =
            new Dictionary<HandlerId, HandlerAssignmentSnapshot>();
        private readonly Dictionary<VehicleId, HandlerId> vehicleOwners =
            new Dictionary<VehicleId, HandlerId>();
        private readonly Dictionary<HandlerId, long> revisions =
            new Dictionary<HandlerId, long>();

        private bool restoreOpen = true;

        public bool TryGet(HandlerId handler, out HandlerAssignmentSnapshot assignment)
        {
            lock (syncRoot)
            {
                return assignments.TryGetValue(handler, out assignment);
            }
        }

        public IReadOnlyList<HandlerAssignmentSnapshot> Snapshot()
        {
            lock (syncRoot)
            {
                List<HandlerAssignmentSnapshot> snapshot =
                    new List<HandlerAssignmentSnapshot>(assignments.Values);
                snapshot.Sort((left, right) => string.CompareOrdinal(
                    left.Handler.ToString(),
                    right.Handler.ToString()));
                return snapshot;
            }
        }

        public AssignmentWriteResult TryConfigure(
            HandlerAssignmentConfiguration configuration,
            long expectedRevision)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            ValidateExpectedRevision(expectedRevision);

            lock (syncRoot)
            {
                restoreOpen = false;

                assignments.TryGetValue(configuration.Handler, out HandlerAssignmentSnapshot current);
                long currentRevision = CurrentRevision(configuration.Handler, current);
                if (expectedRevision != currentRevision)
                {
                    return WriteResult(
                        AssignmentWriteStatus.StaleRevision,
                        currentRevision,
                        "The assignment changed before configuration was applied.");
                }

                if (current != null && current.TripState != HandlerTripState.Idle)
                {
                    return WriteResult(
                        AssignmentWriteStatus.ActiveTrip,
                        currentRevision,
                        "An active trip must finish or recover before reconfiguration.");
                }

                if (vehicleOwners.TryGetValue(configuration.Vehicle, out HandlerId owner) &&
                    owner != configuration.Handler)
                {
                    return WriteResult(
                        AssignmentWriteStatus.DuplicateVehicle,
                        currentRevision,
                        "The vehicle is already assigned to another Handler.");
                }

                long nextRevision = NextRevision(currentRevision);
                HandlerAssignmentSnapshot replacement = new HandlerAssignmentSnapshot(
                    configuration.Handler,
                    configuration.Vehicle,
                    configuration.Destination,
                    configuration.Enabled,
                    configuration.ManuallyHidden,
                    default,
                    HandlerTripState.Idle,
                    nextRevision);

                if (current != null && current.Vehicle != replacement.Vehicle)
                {
                    vehicleOwners.Remove(current.Vehicle);
                }

                assignments[replacement.Handler] = replacement;
                vehicleOwners[replacement.Vehicle] = replacement.Handler;
                revisions[replacement.Handler] = nextRevision;
                return WriteResult(AssignmentWriteStatus.Applied, nextRevision);
            }
        }

        public AssignmentWriteResult TryRestore(HandlerAssignmentSnapshot assignment)
        {
            if (assignment == null)
            {
                throw new ArgumentNullException(nameof(assignment));
            }

            lock (syncRoot)
            {
                if (!restoreOpen)
                {
                    return WriteResult(
                        AssignmentWriteStatus.RestoreConflict,
                        assignment.Revision,
                        "Saved assignments can only be restored before normal registry mutations begin.");
                }

                if (!Enum.IsDefined(typeof(HandlerTripState), assignment.TripState))
                {
                    return WriteResult(
                        AssignmentWriteStatus.RejectedByValidation,
                        assignment.Revision,
                        "The saved assignment contains an unknown trip state.");
                }

                if (assignments.ContainsKey(assignment.Handler))
                {
                    return WriteResult(
                        AssignmentWriteStatus.RestoreConflict,
                        assignments[assignment.Handler].Revision,
                        "A saved assignment for this Handler was already restored.");
                }

                if (vehicleOwners.ContainsKey(assignment.Vehicle))
                {
                    return WriteResult(
                        AssignmentWriteStatus.RestoreConflict,
                        assignment.Revision,
                        "A saved assignment already claims this vehicle.");
                }

                assignments.Add(assignment.Handler, assignment);
                vehicleOwners.Add(assignment.Vehicle, assignment.Handler);
                revisions[assignment.Handler] = assignment.Revision;
                return WriteResult(AssignmentWriteStatus.Applied, assignment.Revision);
            }
        }

        public TripTransitionResult TryTransition(
            HandlerId handler,
            TripId trip,
            HandlerTripState targetState,
            long expectedRevision)
        {
            ValidateExpectedRevision(expectedRevision);

            lock (syncRoot)
            {
                restoreOpen = false;

                if (!assignments.TryGetValue(handler, out HandlerAssignmentSnapshot current))
                {
                    long revision = CurrentRevision(handler, null);
                    return TransitionResult(
                        TripTransitionStatus.IllegalTransition,
                        HandlerTripState.Idle,
                        HandlerTripState.Idle,
                        revision,
                        "No assignment exists for this Handler.");
                }

                if (expectedRevision != current.Revision)
                {
                    return TransitionResult(
                        TripTransitionStatus.StaleRevision,
                        current.TripState,
                        current.TripState,
                        current.Revision,
                        "The assignment changed before the trip transition was applied.");
                }

                if (targetState == current.TripState)
                {
                    bool matchingTrip = current.TripState == HandlerTripState.Idle
                        ? trip.IsEmpty
                        : trip == current.ActiveTrip;
                    return TransitionResult(
                        matchingTrip ? TripTransitionStatus.NoChange : TripTransitionStatus.IllegalTransition,
                        current.TripState,
                        current.TripState,
                        current.Revision,
                        matchingTrip ? string.Empty : "The trip ID does not match the current assignment state.");
                }

                if (!HandlerTripTransitionPolicy.CanTransition(current.TripState, targetState))
                {
                    return TransitionResult(
                        TripTransitionStatus.IllegalTransition,
                        current.TripState,
                        current.TripState,
                        current.Revision,
                        $"The transition {current.TripState} -> {targetState} is not legal.");
                }

                if (current.TripState == HandlerTripState.Idle)
                {
                    if (trip.IsEmpty)
                    {
                        return TransitionResult(
                            TripTransitionStatus.IllegalTransition,
                            current.TripState,
                            current.TripState,
                            current.Revision,
                            "Starting a trip requires a non-empty trip ID.");
                    }
                }
                else if (trip != current.ActiveTrip)
                {
                    return TransitionResult(
                        TripTransitionStatus.IllegalTransition,
                        current.TripState,
                        current.TripState,
                        current.Revision,
                        "The trip ID does not own the active assignment transition.");
                }

                TripId nextTrip = targetState == HandlerTripState.Idle ? default : trip;
                long nextRevision = NextRevision(current.Revision);
                HandlerAssignmentSnapshot replacement = new HandlerAssignmentSnapshot(
                    current.Handler,
                    current.Vehicle,
                    current.Destination,
                    current.Enabled,
                    current.ManuallyHidden,
                    nextTrip,
                    targetState,
                    nextRevision);

                assignments[handler] = replacement;
                revisions[handler] = nextRevision;
                return TransitionResult(
                    TripTransitionStatus.Applied,
                    current.TripState,
                    targetState,
                    nextRevision);
            }
        }

        public AssignmentWriteResult TryRemove(HandlerId handler, long expectedRevision)
        {
            ValidateExpectedRevision(expectedRevision);

            lock (syncRoot)
            {
                restoreOpen = false;

                if (!assignments.TryGetValue(handler, out HandlerAssignmentSnapshot current))
                {
                    return WriteResult(
                        AssignmentWriteStatus.NotFound,
                        CurrentRevision(handler, null),
                        "No assignment exists for this Handler.");
                }

                if (expectedRevision != current.Revision)
                {
                    return WriteResult(
                        AssignmentWriteStatus.StaleRevision,
                        current.Revision,
                        "The assignment changed before removal was applied.");
                }

                if (current.TripState != HandlerTripState.Idle)
                {
                    return WriteResult(
                        AssignmentWriteStatus.ActiveTrip,
                        current.Revision,
                        "An active trip must finish or recover before assignment removal.");
                }

                long nextRevision = NextRevision(current.Revision);
                assignments.Remove(handler);
                vehicleOwners.Remove(current.Vehicle);
                revisions[handler] = nextRevision;
                return WriteResult(AssignmentWriteStatus.Applied, nextRevision);
            }
        }

        private long CurrentRevision(HandlerId handler, HandlerAssignmentSnapshot current)
        {
            if (current != null)
            {
                return current.Revision;
            }

            return revisions.TryGetValue(handler, out long revision) ? revision : 0;
        }

        private static long NextRevision(long revision)
        {
            if (revision == long.MaxValue)
            {
                throw new InvalidOperationException("The assignment revision cannot be incremented further.");
            }

            return revision + 1;
        }

        private static void ValidateExpectedRevision(long expectedRevision)
        {
            if (expectedRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            }
        }

        private static AssignmentWriteResult WriteResult(
            AssignmentWriteStatus status,
            long revision,
            string detail = null) =>
            new AssignmentWriteResult(status, revision, detail);

        private static TripTransitionResult TransitionResult(
            TripTransitionStatus status,
            HandlerTripState previousState,
            HandlerTripState currentState,
            long revision,
            string detail = null) =>
            new TripTransitionResult(status, previousState, currentState, revision, detail);
    }
}
