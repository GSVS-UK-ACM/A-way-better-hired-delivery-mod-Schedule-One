using System;
using System.Collections.Generic;
using VehicleHandlers.Clipboard;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Clipboard.Tests
{
    internal static class Program
    {
        private static int assertionCount;

        private static int Main()
        {
            try
            {
                VerifyInitialAndExistingSelections();
                VerifyCyclingAndConfiguration();
                VerifyUnavailableChoices();
                VerifyVehicleChoicePolicy();
                VerifyValidationBoundaries();

                Console.WriteLine($"Vehicle Handlers clipboard verification passed ({assertionCount} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Clipboard verification failed: {exception.Message}");
                return 1;
            }
        }

        private static void VerifyInitialAndExistingSelections()
        {
            HandlerId handler = Handler(1);
            IReadOnlyList<ClipboardVehicleChoice> vehicles = Vehicles();
            IReadOnlyList<ClipboardPropertyChoice> properties = Properties();
            HandlerClipboardDraft fresh = new HandlerClipboardDraft(
                handler,
                vehicles,
                properties,
                null,
                0);

            Assert(fresh.Handler == handler, "The draft preserves its Handler identity.");
            Assert(fresh.SelectedVehicle.Id == Vehicle(1), "A fresh draft selects the first vehicle.");
            Assert(fresh.SelectedProperty.Key == Property(1), "A fresh draft selects the first property.");
            Assert(fresh.SelectedBay.Key == Bay(1, 1), "A fresh draft selects the first bay.");
            Assert(fresh.Enabled, "A fresh assignment is enabled by default.");
            Assert(!fresh.ManuallyHidden, "A fresh assignment is not manually hidden.");
            Assert(fresh.ExpectedRevision == 0, "A fresh assignment starts at revision zero.");

            HandlerAssignmentSnapshot current = new HandlerAssignmentSnapshot(
                handler,
                Vehicle(2),
                Bay(2, 2),
                false,
                true,
                default,
                HandlerTripState.Idle,
                7);
            HandlerClipboardDraft existing = new HandlerClipboardDraft(
                handler,
                vehicles,
                properties,
                current,
                7);

            Assert(existing.SelectedVehicle.Id == Vehicle(2), "An existing vehicle is preselected.");
            Assert(existing.SelectedProperty.Key == Property(2), "An existing property is preselected.");
            Assert(existing.SelectedBay.Key == Bay(2, 2), "An existing loading bay is preselected.");
            Assert(!existing.Enabled, "The existing enabled state is preserved.");
            Assert(existing.ManuallyHidden, "The existing hidden state is preserved across reconfiguration.");
            Assert(existing.ExpectedRevision == 7, "The authoritative revision is preserved.");

            Assert(existing.Vehicles.Count == 2 && existing.Properties.Count == 2,
                "The draft exposes immutable choice snapshots.");
            AssertThrows<NotSupportedException>(
                () => ((IList<ClipboardVehicleChoice>)existing.Vehicles).Add(
                    new ClipboardVehicleChoice(Vehicle(3), "Third")),
                "Vehicle choices cannot be mutated through the returned list.");
        }

        private static void VerifyCyclingAndConfiguration()
        {
            HandlerClipboardDraft draft = new HandlerClipboardDraft(
                Handler(1),
                Vehicles(),
                Properties(),
                null,
                3);

            draft.CycleVehicle(-1);
            Assert(draft.SelectedVehicle.Id == Vehicle(2), "Vehicle cycling wraps backwards.");
            draft.CycleVehicle(1);
            Assert(draft.SelectedVehicle.Id == Vehicle(1), "Vehicle cycling wraps forwards.");

            draft.CycleProperty(1);
            Assert(draft.SelectedProperty.Key == Property(2), "Property cycling advances the destination.");
            Assert(draft.SelectedBay.Key == Bay(2, 1), "Changing property resets to its first bay.");
            draft.CycleBay(1);
            Assert(draft.SelectedBay.Key == Bay(2, 2), "Bay cycling advances within the property.");
            draft.CycleBay(1);
            Assert(draft.SelectedBay.Key == Bay(2, 1), "Bay cycling wraps within the property.");
            draft.CycleProperty(1);
            Assert(draft.SelectedProperty.Key == Property(1), "Property cycling wraps forwards.");

            Assert(draft.TrySelectVehicle(Vehicle(2)), "A known vehicle can be selected by stable ID.");
            Assert(!draft.TrySelectVehicle(Vehicle(9)), "An unknown vehicle cannot be selected.");
            Assert(draft.TrySelectDestination(Bay(2, 2)),
                "A destination can be selected only by its complete stable bay identity.");
            Assert(!draft.TrySelectDestination(Bay(2, 9)), "An unknown bay cannot be selected.");

            draft.Enabled = false;
            Assert(draft.TryCreateConfiguration(
                out HandlerAssignmentConfiguration configuration,
                out string detail),
                "A complete draft creates an assignment configuration.");
            Assert(string.IsNullOrEmpty(detail), "A complete draft has no validation detail.");
            Assert(configuration.Handler == Handler(1), "Configuration keeps the Handler ID.");
            Assert(configuration.Vehicle == Vehicle(2), "Configuration keeps the selected vehicle ID.");
            Assert(configuration.Destination == Bay(2, 2), "Configuration keeps the selected bay identity.");
            Assert(!configuration.Enabled && !configuration.ManuallyHidden,
                "Configuration keeps its control flags.");

            draft.ObserveRevision(8);
            Assert(draft.ExpectedRevision == 8, "A write result advances the draft revision.");
        }

        private static void VerifyUnavailableChoices()
        {
            HandlerClipboardDraft noVehicles = new HandlerClipboardDraft(
                Handler(1),
                Array.Empty<ClipboardVehicleChoice>(),
                Properties(),
                null,
                0);
            Assert(noVehicles.SelectedVehicle == null, "An empty vehicle list has no selection.");
            Assert(!noVehicles.TryCreateConfiguration(out _, out string vehicleDetail) &&
                vehicleDetail.Contains("vehicle", StringComparison.OrdinalIgnoreCase),
                "Configuration explains when no owned vehicle is available.");
            noVehicles.CycleVehicle(1);
            Assert(noVehicles.SelectedVehicle == null, "Cycling an empty vehicle list is safe.");

            HandlerClipboardDraft noProperties = new HandlerClipboardDraft(
                Handler(1),
                Vehicles(),
                Array.Empty<ClipboardPropertyChoice>(),
                null,
                0);
            Assert(noProperties.SelectedProperty == null && noProperties.SelectedBay == null,
                "An empty property list has no property or bay selection.");
            Assert(!noProperties.TryCreateConfiguration(out _, out string propertyDetail) &&
                propertyDetail.Contains("destination", StringComparison.OrdinalIgnoreCase),
                "Configuration explains when no destination bay is available.");
            noProperties.CycleProperty(-1);
            noProperties.CycleBay(1);
            Assert(noProperties.SelectedBay == null, "Cycling empty destination choices is safe.");
        }

        private static void VerifyValidationBoundaries()
        {
            AssertThrows<ArgumentException>(
                () => new ClipboardVehicleChoice(default, "Missing"),
                "Vehicle choices reject an empty ID.");
            AssertThrows<ArgumentException>(
                () => new ClipboardBayChoice(default, "Missing"),
                "Bay choices reject an empty identity.");
            AssertThrows<ArgumentNullException>(
                () => new ClipboardPropertyChoice(Property(1), "Property", null),
                "Property choices reject a null bay list.");
            AssertThrows<ArgumentException>(
                () => new HandlerClipboardDraft(default, Vehicles(), Properties(), null, 0),
                "Drafts reject an empty Handler ID.");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new HandlerClipboardDraft(Handler(1), Vehicles(), Properties(), null, -1),
                "Drafts reject a negative revision.");
            AssertThrows<ArgumentException>(
                () => new HandlerClipboardDraft(
                    Handler(1),
                    Vehicles(),
                    Properties(),
                    new HandlerAssignmentSnapshot(
                        Handler(2),
                        Vehicle(1),
                        Bay(1, 1),
                        true,
                        false,
                        default,
                        HandlerTripState.Idle,
                        1),
                    1),
                "A draft rejects another Handler's current assignment.");

            HandlerClipboardDraft draft = new HandlerClipboardDraft(
                Handler(1), Vehicles(), Properties(), null, 0);
            AssertThrows<ArgumentOutOfRangeException>(
                () => draft.ObserveRevision(-1),
                "Drafts reject a negative observed revision.");
        }

        private static void VerifyVehicleChoicePolicy()
        {
            HandlerId currentHandler = Handler(1);
            HandlerAssignmentSnapshot ownAssignment = new HandlerAssignmentSnapshot(
                currentHandler,
                Vehicle(1),
                Bay(1, 1),
                true,
                false,
                default,
                HandlerTripState.Idle,
                1);
            HandlerAssignmentSnapshot otherAssignment = new HandlerAssignmentSnapshot(
                Handler(2),
                Vehicle(2),
                Bay(2, 1),
                true,
                false,
                default,
                HandlerTripState.Idle,
                1);
            IReadOnlyList<HandlerAssignmentSnapshot> assignments =
                new[] { ownAssignment, otherAssignment };

            Assert(HandlerClipboardChoicePolicy.IsVehicleAvailableForHandler(
                Vehicle(1), currentHandler, assignments),
                "The current Handler keeps its assigned vehicle in the chooser.");
            Assert(!HandlerClipboardChoicePolicy.IsVehicleAvailableForHandler(
                Vehicle(2), currentHandler, assignments),
                "A vehicle assigned to another Handler is removed from the chooser.");
            Assert(HandlerClipboardChoicePolicy.IsVehicleAvailableForHandler(
                Vehicle(3), currentHandler, assignments),
                "An unassigned vehicle remains available in the chooser.");

            string located = HandlerClipboardChoicePolicy.FormatVehicleDisplayName(
                "Veeper", Vehicle(1), "Loading Dock 1", "Storage Unit");
            Assert(located == "Veeper (Loading Dock 1, Storage Unit)",
                "A dock occupant is labelled with its loading bay and property.");
            string fallback = HandlerClipboardChoicePolicy.FormatVehicleDisplayName(
                "Bruiser", Vehicle(2), string.Empty, string.Empty);
            Assert(fallback.StartsWith("Bruiser (") && fallback.EndsWith(")"),
                "An unlocated vehicle keeps a short stable-ID fallback label.");
        }

        private static IReadOnlyList<ClipboardVehicleChoice> Vehicles() =>
            new List<ClipboardVehicleChoice>
            {
                new ClipboardVehicleChoice(Vehicle(1), "Coupe"),
                new ClipboardVehicleChoice(Vehicle(2), "Van")
            };

        private static IReadOnlyList<ClipboardPropertyChoice> Properties() =>
            new List<ClipboardPropertyChoice>
            {
                new ClipboardPropertyChoice(
                    Property(1),
                    "Barn",
                    new List<ClipboardBayChoice>
                    {
                        new ClipboardBayChoice(Bay(1, 1), "Barn bay 1")
                    }),
                new ClipboardPropertyChoice(
                    Property(2),
                    "Warehouse",
                    new List<ClipboardBayChoice>
                    {
                        new ClipboardBayChoice(Bay(2, 1), "Warehouse bay 1"),
                        new ClipboardBayChoice(Bay(2, 2), "Warehouse bay 2")
                    })
            };

        private static HandlerId Handler(int value) =>
            new HandlerId(new Guid(value, 0, 0, new byte[8]));

        private static VehicleId Vehicle(int value) =>
            new VehicleId(new Guid(1000 + value, 0, 0, new byte[8]));

        private static PropertyKey Property(int value) =>
            new PropertyKey($"PROPERTY-{value}");

        private static LoadingBayKey Bay(int property, int bay) =>
            new LoadingBayKey(
                Property(property),
                new DockId(new Guid((property * 1000) + bay, 0, 0, new byte[8])),
                bay - 1);

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
