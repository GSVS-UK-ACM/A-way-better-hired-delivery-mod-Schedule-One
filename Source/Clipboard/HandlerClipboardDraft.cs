using System;
using System.Collections.Generic;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Clipboard
{
    internal static class HandlerClipboardChoicePolicy
    {
        public static bool IsVehicleAvailableForHandler(
            VehicleId vehicle,
            HandlerId handler,
            IReadOnlyList<HandlerAssignmentSnapshot> assignments)
        {
            if (vehicle.IsEmpty)
            {
                throw new ArgumentException("A vehicle is required.", nameof(vehicle));
            }

            if (handler.IsEmpty)
            {
                throw new ArgumentException("A Handler is required.", nameof(handler));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            for (int index = 0; index < assignments.Count; index++)
            {
                HandlerAssignmentSnapshot assignment = assignments[index];
                if (assignment != null && assignment.Vehicle == vehicle)
                {
                    return assignment.Handler == handler;
                }
            }

            return true;
        }

        public static string FormatVehicleDisplayName(
            string vehicleName,
            VehicleId vehicle,
            string dockName,
            string propertyName)
        {
            if (vehicle.IsEmpty)
            {
                throw new ArgumentException("A vehicle is required.", nameof(vehicle));
            }

            string name = string.IsNullOrWhiteSpace(vehicleName)
                ? "Owned vehicle"
                : vehicleName.Trim();
            if (!string.IsNullOrWhiteSpace(dockName) &&
                !string.IsNullOrWhiteSpace(propertyName))
            {
                return $"{name} ({dockName.Trim()}, {propertyName.Trim()})";
            }

            return $"{name} ({vehicle.ToString().Substring(0, 8)})";
        }
    }

    internal sealed class ClipboardVehicleChoice
    {
        public ClipboardVehicleChoice(VehicleId id, string displayName)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A vehicle choice requires an ID.", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName.Trim();
        }

        public VehicleId Id { get; }

        public string DisplayName { get; }
    }

    internal sealed class ClipboardBayChoice
    {
        public ClipboardBayChoice(LoadingBayKey key, string displayName)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A loading-bay choice requires an identity.", nameof(key));
            }

            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key.ToString() : displayName.Trim();
        }

        public LoadingBayKey Key { get; }

        public string DisplayName { get; }
    }

    internal sealed class ClipboardPropertyChoice
    {
        private readonly IReadOnlyList<ClipboardBayChoice> bays;

        public ClipboardPropertyChoice(
            PropertyKey key,
            string displayName,
            IReadOnlyList<ClipboardBayChoice> bays)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A property choice requires an identity.", nameof(key));
            }

            if (bays == null)
            {
                throw new ArgumentNullException(nameof(bays));
            }

            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key.ToString() : displayName.Trim();
            this.bays = Copy(bays);
        }

        public PropertyKey Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<ClipboardBayChoice> Bays => bays;

        private static IReadOnlyList<ClipboardBayChoice> Copy(
            IReadOnlyList<ClipboardBayChoice> source)
        {
            List<ClipboardBayChoice> copy = new List<ClipboardBayChoice>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                ClipboardBayChoice choice = source[index];
                if (choice == null)
                {
                    throw new ArgumentException("A property cannot contain a null loading-bay choice.", nameof(source));
                }

                copy.Add(choice);
            }

            return copy.AsReadOnly();
        }
    }

    internal sealed class HandlerClipboardDraft
    {
        private readonly IReadOnlyList<ClipboardVehicleChoice> vehicles;
        private readonly IReadOnlyList<ClipboardPropertyChoice> properties;
        private int vehicleIndex;
        private int propertyIndex;
        private int bayIndex;

        public HandlerClipboardDraft(
            HandlerId handler,
            IReadOnlyList<ClipboardVehicleChoice> vehicles,
            IReadOnlyList<ClipboardPropertyChoice> properties,
            HandlerAssignmentSnapshot current,
            long expectedRevision)
        {
            if (handler.IsEmpty)
            {
                throw new ArgumentException("A clipboard draft requires a Handler.", nameof(handler));
            }

            if (vehicles == null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            if (expectedRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            }

            if (current != null && current.Handler != handler)
            {
                throw new ArgumentException("The current assignment belongs to another Handler.", nameof(current));
            }

            Handler = handler;
            this.vehicles = CopyVehicles(vehicles);
            this.properties = CopyProperties(properties);
            ExpectedRevision = expectedRevision;
            Enabled = current == null || current.Enabled;
            ManuallyHidden = current != null && current.ManuallyHidden;

            if (current != null)
            {
                TrySelectVehicle(current.Vehicle);
                TrySelectDestination(current.Destination);
            }
        }

        public HandlerId Handler { get; }

        public IReadOnlyList<ClipboardVehicleChoice> Vehicles => vehicles;

        public IReadOnlyList<ClipboardPropertyChoice> Properties => properties;

        public ClipboardVehicleChoice SelectedVehicle =>
            vehicles.Count == 0 ? null : vehicles[vehicleIndex];

        public ClipboardPropertyChoice SelectedProperty =>
            properties.Count == 0 ? null : properties[propertyIndex];

        public ClipboardBayChoice SelectedBay
        {
            get
            {
                ClipboardPropertyChoice property = SelectedProperty;
                return property == null || property.Bays.Count == 0 ? null : property.Bays[bayIndex];
            }
        }

        public bool Enabled { get; set; }

        public bool ManuallyHidden { get; }

        public long ExpectedRevision { get; private set; }

        public void CycleVehicle(int offset)
        {
            vehicleIndex = Wrap(vehicleIndex, offset, vehicles.Count);
        }

        public void CycleProperty(int offset)
        {
            propertyIndex = Wrap(propertyIndex, offset, properties.Count);
            bayIndex = 0;
        }

        public void CycleBay(int offset)
        {
            ClipboardPropertyChoice property = SelectedProperty;
            bayIndex = Wrap(bayIndex, offset, property == null ? 0 : property.Bays.Count);
        }

        public bool TrySelectVehicle(VehicleId vehicle)
        {
            for (int index = 0; index < vehicles.Count; index++)
            {
                if (vehicles[index].Id == vehicle)
                {
                    vehicleIndex = index;
                    return true;
                }
            }

            return false;
        }

        public bool TrySelectDestination(LoadingBayKey destination)
        {
            for (int destinationIndex = 0; destinationIndex < properties.Count; destinationIndex++)
            {
                ClipboardPropertyChoice property = properties[destinationIndex];
                if (property.Key != destination.Property)
                {
                    continue;
                }

                for (int destinationBayIndex = 0;
                    destinationBayIndex < property.Bays.Count;
                    destinationBayIndex++)
                {
                    if (property.Bays[destinationBayIndex].Key == destination)
                    {
                        propertyIndex = destinationIndex;
                        bayIndex = destinationBayIndex;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryCreateConfiguration(
            out HandlerAssignmentConfiguration configuration,
            out string detail)
        {
            ClipboardVehicleChoice vehicle = SelectedVehicle;
            if (vehicle == null)
            {
                configuration = null;
                detail = "No owned vehicle is available.";
                return false;
            }

            ClipboardBayChoice bay = SelectedBay;
            if (bay == null)
            {
                configuration = null;
                detail = "No owned destination with a loading bay is available.";
                return false;
            }

            configuration = new HandlerAssignmentConfiguration(
                Handler,
                vehicle.Id,
                bay.Key,
                Enabled,
                ManuallyHidden);
            detail = string.Empty;
            return true;
        }

        public void ObserveRevision(long revision)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            ExpectedRevision = revision;
        }

        private static int Wrap(int current, int offset, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = (current + offset) % count;
            return result < 0 ? result + count : result;
        }

        private static IReadOnlyList<ClipboardVehicleChoice> CopyVehicles(
            IReadOnlyList<ClipboardVehicleChoice> source)
        {
            List<ClipboardVehicleChoice> copy = new List<ClipboardVehicleChoice>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                ClipboardVehicleChoice choice = source[index];
                if (choice == null)
                {
                    throw new ArgumentException("Vehicle choices cannot contain null.", nameof(source));
                }

                copy.Add(choice);
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<ClipboardPropertyChoice> CopyProperties(
            IReadOnlyList<ClipboardPropertyChoice> source)
        {
            List<ClipboardPropertyChoice> copy = new List<ClipboardPropertyChoice>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                ClipboardPropertyChoice choice = source[index];
                if (choice == null)
                {
                    throw new ArgumentException("Property choices cannot contain null.", nameof(source));
                }

                copy.Add(choice);
            }

            return copy.AsReadOnly();
        }
    }
}
