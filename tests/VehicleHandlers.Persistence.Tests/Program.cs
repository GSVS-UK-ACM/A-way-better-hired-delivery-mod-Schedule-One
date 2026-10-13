using System;
using System.Collections.Generic;
using System.IO;
using VehicleHandlers.Assignments;
using VehicleHandlers.Contracts;
using VehicleHandlers.Persistence;

namespace VehicleHandlers.Persistence.Tests
{
    internal static class Program
    {
        private static int assertionCount;

        private static int Main()
        {
            string temporaryRoot = Path.Combine(
                Environment.CurrentDirectory,
                "artifacts",
                "persistence-tests",
                Guid.NewGuid().ToString("N"));
            try
            {
                VerifyRoundTrip();
                VerifyInvalidDocuments();
                VerifyAtomicFileLifecycle(temporaryRoot);
                VerifyRegistryRestore();
                Console.WriteLine(
                    $"Vehicle Handlers persistence verification passed ({assertionCount} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Persistence verification failed: {exception.Message}");
                return 1;
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, true);
                }
            }
        }

        private static void VerifyRoundTrip()
        {
            HandlerAssignmentSnapshot idle = Assignment(1, 1, HandlerTripState.Idle, default, 4);
            TripId trip = new TripId(new Guid(9001, 0, 0, new byte[8]));
            HandlerAssignmentSnapshot active = Assignment(
                2,
                2,
                HandlerTripState.WaitingForBay,
                trip,
                9);
            string json = AssignmentPersistenceCodec.Serialize(new[] { idle, active });

            Assert(json.Contains("\"schemaVersion\": 1", StringComparison.Ordinal),
                "The document writes schema version 1.");
            Assert(!json.Contains("SavePath", StringComparison.OrdinalIgnoreCase),
                "The document does not persist a local save path.");
            Assert(AssignmentPersistenceCodec.TryDeserialize(
                json,
                out IReadOnlyList<HandlerAssignmentSnapshot> restored,
                out string detail),
                $"A valid document round-trips: {detail}");
            Assert(restored.Count == 2, "Round-trip preserves the assignment count.");
            Assert(restored[0].Handler == idle.Handler && restored[0].Vehicle == idle.Vehicle,
                "Round-trip preserves stable Handler and vehicle IDs.");
            Assert(restored[0].Destination == idle.Destination,
                "Round-trip preserves property, dock, and dock-index identity.");
            Assert(restored[0].Enabled == idle.Enabled &&
                restored[0].ManuallyHidden == idle.ManuallyHidden &&
                restored[0].Revision == idle.Revision,
                "Round-trip preserves flags and revision.");
            Assert(restored[1].TripState == HandlerTripState.WaitingForBay &&
                restored[1].ActiveTrip == trip,
                "Round-trip preserves recoverable active-trip state.");
        }

        private static void VerifyInvalidDocuments()
        {
            HandlerAssignmentSnapshot idle = Assignment(1, 1, HandlerTripState.Idle, default, 1);
            string valid = AssignmentPersistenceCodec.Serialize(new[] { idle });

            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                string.Empty,
                out _,
                out string emptyDetail) && emptyDetail.Contains("empty", StringComparison.OrdinalIgnoreCase),
                "An empty document is rejected with a useful diagnostic.");
            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                valid.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal),
                out _,
                out string versionDetail) && versionDetail.Contains("Unsupported", StringComparison.Ordinal),
                "A future schema version is rejected safely.");
            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                valid.Replace(idle.Handler.ToString(), "not-a-guid", StringComparison.Ordinal),
                out _,
                out string handlerDetail) && handlerDetail.Contains("Handler ID", StringComparison.Ordinal),
                "An invalid Handler ID is rejected.");
            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                valid.Replace("\"dockIndex\": 0", "\"dockIndex\": -1", StringComparison.Ordinal),
                out _,
                out string dockDetail) && dockDetail.Contains("Dock index", StringComparison.Ordinal),
                "A negative dock index is rejected.");
            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                valid.Replace("\"tripState\": 0", "\"tripState\": 1", StringComparison.Ordinal),
                out _,
                out string tripDetail) && tripDetail.Contains("inconsistent", StringComparison.Ordinal),
                "A non-idle state without a trip ID is rejected.");
            Assert(!AssignmentPersistenceCodec.TryDeserialize(
                "{ definitely not json }",
                out _,
                out string jsonDetail) && jsonDetail.Contains("invalid", StringComparison.OrdinalIgnoreCase),
                "Malformed JSON is rejected without escaping an exception.");
        }

        private static void VerifyAtomicFileLifecycle(string temporaryRoot)
        {
            string path = AssignmentPersistenceFile.BuildPath(temporaryRoot);
            Assert(path == Path.Combine(
                Path.GetFullPath(temporaryRoot),
                "UserData",
                "VehicleHandlers.json"),
                "Persistence uses the per-save UserData document path.");
            Assert(AssignmentPersistenceFile.TryLoad(
                path,
                out IReadOnlyList<HandlerAssignmentSnapshot> missing,
                out bool missingRecovered,
                out _),
                "A missing first-run document loads safely.");
            Assert(missing.Count == 0 && !missingRecovered,
                "A missing document restores an empty assignment set.");

            HandlerAssignmentSnapshot first = Assignment(1, 1, HandlerTripState.Idle, default, 1);
            AssignmentPersistenceFile.Save(path, new[] { first }, out bool firstChanged);
            Assert(firstChanged && File.Exists(path), "First save writes the persistence document.");
            Assert(!File.Exists(path + ".tmp"), "Atomic save leaves no temporary document behind.");

            AssignmentPersistenceFile.Save(path, new[] { first }, out bool unchanged);
            Assert(!unchanged, "An identical snapshot does not rewrite the document.");

            HandlerAssignmentSnapshot second = Assignment(2, 2, HandlerTripState.Idle, default, 2);
            AssignmentPersistenceFile.Save(path, new[] { second }, out bool secondChanged);
            Assert(secondChanged && File.Exists(path + ".bak"),
                "A changed save preserves the previous valid document as backup.");

            File.WriteAllText(path, "corrupt");
            Assert(AssignmentPersistenceFile.TryLoad(
                path,
                out IReadOnlyList<HandlerAssignmentSnapshot> recovered,
                out bool recoveredBackup,
                out string recoveryDetail),
                $"A corrupt primary document recovers from backup: {recoveryDetail}");
            Assert(recoveredBackup && recovered.Count == 1 && recovered[0].Handler == first.Handler,
                "Backup recovery returns the previous valid assignment snapshot.");
        }

        private static void VerifyRegistryRestore()
        {
            HandlerAssignmentSnapshot first = Assignment(1, 1, HandlerTripState.Idle, default, 3);
            HandlerAssignmentSnapshot second = Assignment(2, 2, HandlerTripState.Idle, default, 7);
            string json = AssignmentPersistenceCodec.Serialize(new[] { first, second });
            Assert(AssignmentPersistenceCodec.TryDeserialize(json, out var restored, out _),
                "Registry fixture deserializes.");

            HandlerAssignmentRegistry registry = new HandlerAssignmentRegistry();
            foreach (HandlerAssignmentSnapshot assignment in restored)
            {
                Assert(registry.TryRestore(assignment).Applied,
                    "A decoded assignment enters the registry only through TryRestore.");
            }

            Assert(registry.Snapshot().Count == 2,
                "Restore rebuilds the authoritative registry snapshot.");
            Assert(registry.TryRestore(first).Status == AssignmentWriteStatus.RestoreConflict,
                "Restore conflicts remain enforced after persistence decoding.");
        }

        private static HandlerAssignmentSnapshot Assignment(
            int handler,
            int vehicle,
            HandlerTripState state,
            TripId trip,
            long revision) =>
            new HandlerAssignmentSnapshot(
                new HandlerId(new Guid(handler, 0, 0, new byte[8])),
                new VehicleId(new Guid(1000 + vehicle, 0, 0, new byte[8])),
                new LoadingBayKey(
                    new PropertyKey($"PROPERTY-{handler}"),
                    new DockId(new Guid(2000 + handler, 0, 0, new byte[8])),
                    handler - 1),
                handler % 2 == 1,
                handler % 2 == 0,
                trip,
                state,
                revision);

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
