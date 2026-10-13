using System.Collections.Generic;
using MelonLoader;
using VehicleHandlers.Contracts;
using VehicleHandlers.Employees;

namespace VehicleHandlers.Assignments
{
    internal static class HandlerAssignmentRuntime
    {
        private static HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
        private static readonly Dictionary<HandlerId, long> knownRevisions =
            new Dictionary<HandlerId, long>();

        public static IHandlerAssignmentRegistry Registry => registry;

        public static void Initialize()
        {
            HandlerLifecycleBridge.SetSink(new AssignmentLifecycleSink());
        }

        public static void BeginRestore()
        {
            registry = new HandlerAssignmentRegistry();
            knownRevisions.Clear();
        }

        public static long GetExpectedRevision(
            HandlerId handler,
            HandlerAssignmentSnapshot current)
        {
            if (current != null)
            {
                ObserveRevision(handler, current.Revision);
                return current.Revision;
            }

            return knownRevisions.TryGetValue(handler, out long revision) ? revision : 0;
        }

        public static void ObserveRevision(HandlerId handler, long revision)
        {
            if (revision >= 0)
            {
                knownRevisions[handler] = revision;
            }
        }

        private sealed class AssignmentLifecycleSink : IHandlerLifecycleSink
        {
            public void HandlerUnavailable(HandlerId handler, HandlerUnavailableReason reason)
            {
                if (!registry.TryGet(handler, out HandlerAssignmentSnapshot assignment))
                {
                    return;
                }

                AssignmentWriteResult result = registry.TryRemove(handler, assignment.Revision);
                ObserveRevision(handler, result.Revision);
                if (result.Applied)
                {
                    MelonLogger.Msg($"Removed Vehicle Handler assignment for {handler} after {reason}.");
                    return;
                }

                MelonLogger.Warning(
                    $"Could not remove Vehicle Handler assignment for {handler} after {reason}: " +
                    $"{result.Status} {result.Detail}");
            }

            public void ModUnloading()
            {
                // Normal game saves own persistence; shutdown performs no extra file mutation.
            }
        }
    }
}
