using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Persistence
{
    internal static class AssignmentPersistenceCodec
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaximumAssignments = 256;

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static string Serialize(IReadOnlyList<HandlerAssignmentSnapshot> assignments)
        {
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (assignments.Count > MaximumAssignments)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(assignments),
                    $"At most {MaximumAssignments} assignments can be persisted.");
            }

            PersistedAssignmentDocument document = new PersistedAssignmentDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Assignments = new List<PersistedAssignment>(assignments.Count)
            };

            for (int index = 0; index < assignments.Count; index++)
            {
                HandlerAssignmentSnapshot assignment = assignments[index] ??
                    throw new ArgumentException("Assignments cannot contain null entries.", nameof(assignments));
                document.Assignments.Add(PersistedAssignment.FromSnapshot(assignment));
            }

            return JsonSerializer.Serialize(document, jsonOptions);
        }

        public static bool TryDeserialize(
            string json,
            out IReadOnlyList<HandlerAssignmentSnapshot> assignments,
            out string detail)
        {
            assignments = Array.Empty<HandlerAssignmentSnapshot>();
            detail = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                detail = "The persistence document is empty.";
                return false;
            }

            try
            {
                PersistedAssignmentDocument document =
                    JsonSerializer.Deserialize<PersistedAssignmentDocument>(json, jsonOptions);
                if (document == null)
                {
                    detail = "The persistence document has no root object.";
                    return false;
                }

                if (document.SchemaVersion != CurrentSchemaVersion)
                {
                    detail = $"Unsupported persistence schema version {document.SchemaVersion}; " +
                        $"expected {CurrentSchemaVersion}.";
                    return false;
                }

                if (document.Assignments == null)
                {
                    detail = "The persistence document has no assignments array.";
                    return false;
                }

                if (document.Assignments.Count > MaximumAssignments)
                {
                    detail = $"The persistence document exceeds the {MaximumAssignments}-assignment limit.";
                    return false;
                }

                List<HandlerAssignmentSnapshot> restored =
                    new List<HandlerAssignmentSnapshot>(document.Assignments.Count);
                for (int index = 0; index < document.Assignments.Count; index++)
                {
                    PersistedAssignment persisted = document.Assignments[index];
                    if (persisted == null || !persisted.TryCreateSnapshot(out HandlerAssignmentSnapshot snapshot, out detail))
                    {
                        detail = $"Assignment {index}: {detail}";
                        return false;
                    }

                    restored.Add(snapshot);
                }

                assignments = restored.AsReadOnly();
                return true;
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is NotSupportedException ||
                exception is ArgumentException ||
                exception is OverflowException)
            {
                detail = $"The persistence document is invalid: {exception.Message}";
                return false;
            }
        }
    }

    internal static class AssignmentPersistenceFile
    {
        private static readonly Encoding utf8 = new UTF8Encoding(false);

        public static bool TryLoad(
            string path,
            out IReadOnlyList<HandlerAssignmentSnapshot> assignments,
            out bool recoveredBackup,
            out string detail)
        {
            ValidatePath(path);
            recoveredBackup = false;
            if (!File.Exists(path))
            {
                assignments = Array.Empty<HandlerAssignmentSnapshot>();
                detail = "No persistence document exists yet.";
                return true;
            }

            string json = File.ReadAllText(path, utf8);
            if (AssignmentPersistenceCodec.TryDeserialize(json, out assignments, out detail))
            {
                return true;
            }

            string backupPath = BackupPath(path);
            if (!File.Exists(backupPath))
            {
                return false;
            }

            string backupJson = File.ReadAllText(backupPath, utf8);
            if (!AssignmentPersistenceCodec.TryDeserialize(
                backupJson,
                out assignments,
                out string backupDetail))
            {
                detail = $"Primary document failed ({detail}); backup failed ({backupDetail}).";
                return false;
            }

            recoveredBackup = true;
            detail = "Recovered assignments from the previous valid backup document.";
            return true;
        }

        public static bool Save(
            string path,
            IReadOnlyList<HandlerAssignmentSnapshot> assignments,
            out bool changed)
        {
            ValidatePath(path);
            string json = AssignmentPersistenceCodec.Serialize(assignments);
            if (File.Exists(path) && string.Equals(
                File.ReadAllText(path, utf8),
                json,
                StringComparison.Ordinal))
            {
                changed = false;
                return true;
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("The persistence path must include a directory.", nameof(path));
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = TemporaryPath(path);
            try
            {
                File.WriteAllText(temporaryPath, json, utf8);
                if (File.Exists(path))
                {
                    File.Copy(path, BackupPath(path), true);
                }

                File.Move(temporaryPath, path, true);
                changed = true;
                return true;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public static string BuildPath(string saveRoot)
        {
            if (string.IsNullOrWhiteSpace(saveRoot))
            {
                throw new ArgumentException("A save root is required.", nameof(saveRoot));
            }

            return Path.Combine(
                Path.GetFullPath(saveRoot),
                "UserData",
                "VehicleHandlers.json");
        }

        private static string BackupPath(string path) => path + ".bak";

        private static string TemporaryPath(string path) => path + ".tmp";

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A persistence file path is required.", nameof(path));
            }
        }
    }

    internal sealed class PersistedAssignmentDocument
    {
        public int SchemaVersion { get; set; }

        public List<PersistedAssignment> Assignments { get; set; }
    }

    internal sealed class PersistedAssignment
    {
        public string HandlerId { get; set; }

        public string VehicleId { get; set; }

        public string PropertyCode { get; set; }

        public string DockId { get; set; }

        public int DockIndex { get; set; }

        public bool Enabled { get; set; }

        public bool ManuallyHidden { get; set; }

        public string ActiveTripId { get; set; }

        public int TripState { get; set; }

        public long Revision { get; set; }

        public static PersistedAssignment FromSnapshot(HandlerAssignmentSnapshot assignment) =>
            new PersistedAssignment
            {
                HandlerId = assignment.Handler.ToString(),
                VehicleId = assignment.Vehicle.ToString(),
                PropertyCode = assignment.Destination.Property.ToString(),
                DockId = assignment.Destination.Dock.ToString(),
                DockIndex = assignment.Destination.DockIndex,
                Enabled = assignment.Enabled,
                ManuallyHidden = assignment.ManuallyHidden,
                ActiveTripId = assignment.ActiveTrip.IsEmpty ? null : assignment.ActiveTrip.ToString(),
                TripState = (int)assignment.TripState,
                Revision = assignment.Revision
            };

        public bool TryCreateSnapshot(
            out HandlerAssignmentSnapshot snapshot,
            out string detail)
        {
            snapshot = null;
            detail = string.Empty;
            if (!VehicleHandlers.Contracts.HandlerId.TryParse(
                this.HandlerId,
                out VehicleHandlers.Contracts.HandlerId handler))
            {
                detail = "Handler ID is missing or invalid.";
                return false;
            }

            if (!VehicleHandlers.Contracts.VehicleId.TryParse(
                this.VehicleId,
                out VehicleHandlers.Contracts.VehicleId vehicle))
            {
                detail = "Vehicle ID is missing or invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PropertyCode))
            {
                detail = "Property code is missing.";
                return false;
            }

            if (!VehicleHandlers.Contracts.DockId.TryParse(
                DockId,
                out VehicleHandlers.Contracts.DockId dock))
            {
                detail = "Dock ID is missing or invalid.";
                return false;
            }

            if (DockIndex < 0)
            {
                detail = "Dock index cannot be negative.";
                return false;
            }

            if (Revision < 0)
            {
                detail = "Revision cannot be negative.";
                return false;
            }

            if (!Enum.IsDefined(typeof(HandlerTripState), TripState))
            {
                detail = $"Trip state {TripState} is unknown.";
                return false;
            }

            HandlerTripState state = (HandlerTripState)TripState;
            TripId trip = default;
            if (!string.IsNullOrWhiteSpace(ActiveTripId) &&
                !VehicleHandlers.Contracts.TripId.TryParse(ActiveTripId, out trip))
            {
                detail = "Active trip ID is invalid.";
                return false;
            }

            bool hasTrip = !trip.IsEmpty;
            if ((state == HandlerTripState.Idle && hasTrip) ||
                (state != HandlerTripState.Idle && !hasTrip))
            {
                detail = "Trip ID and trip state are inconsistent.";
                return false;
            }

            snapshot = new HandlerAssignmentSnapshot(
                handler,
                vehicle,
                new LoadingBayKey(new PropertyKey(PropertyCode), dock, DockIndex),
                Enabled,
                ManuallyHidden,
                trip,
                state,
                Revision);
            return true;
        }
    }
}
