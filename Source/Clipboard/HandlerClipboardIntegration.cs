using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppScheduleOne.Delivery;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Tools;
using Il2CppScheduleOne.Vehicles;
using MelonLoader;
using UnityEngine;
using VehicleHandlers.Assignments;
using VehicleHandlers.Contracts;
using VehicleHandlers.Employees;

namespace VehicleHandlers.Clipboard
{
    internal static class HandlerClipboardSession
    {
        private const float PanelWidth = 680f;
        private const float PanelHeight = 470f;
        private const float Margin = 20f;
        private const float RowHeight = 42f;

        private static Packager worker;
        private static HandlerClipboardDraft draft;
        private static HandlerId handler;
        private static bool active;
        private static bool drawFailureLogged;
        private static string status = string.Empty;

        public static bool IsActive => active;

        public static void Begin(Il2CppSystem.Collections.Generic.List<IConfigurable> configurables)
        {
            End();
            if (configurables == null || configurables.Count != 1)
            {
                return;
            }

            IConfigurable configurable = configurables[0];
            Packager selected = (configurable as Il2CppObjectBase)?.TryCast<Packager>();
            if (selected == null ||
                !HandlerLifecycleFilter.TryGetMarker(selected, out HandlerEmployee _))
            {
                return;
            }

            active = true;
            worker = selected;
            if (!HandlerId.TryParse(selected.GUID.ToString(), out handler))
            {
                status = "This Vehicle Handler does not expose a valid persistent GUID.";
                return;
            }

            Refresh(false);
        }

        public static void End()
        {
            active = false;
            worker = null;
            draft = null;
            handler = default;
            status = string.Empty;
            drawFailureLogged = false;
        }

        public static void HideVanillaPanel(ManagementInterface management)
        {
            if (!active || management == null || management.loadedPanel == null)
            {
                return;
            }

            management.loadedPanel.gameObject.SetActive(false);
        }

        public static void Draw()
        {
            if (!active)
            {
                return;
            }

            try
            {
                DrawPanel();
            }
            catch (Exception exception)
            {
                if (!drawFailureLogged)
                {
                    drawFailureLogged = true;
                    MelonLogger.Error($"Vehicle Handler clipboard UI failed safely: {exception.Message}");
                }
            }
        }

        private static void DrawPanel()
        {
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            try
            {
                float width = Math.Min(PanelWidth, Math.Max(320f, Screen.width - (Margin * 2f)));
                float height = Math.Min(PanelHeight, Math.Max(360f, Screen.height - (Margin * 2f)));
                float left = (Screen.width - width) / 2f;
                float top = (Screen.height - height) / 2f;
                Rect panel = new Rect(left, top, width, height);
                GUI.Box(panel, string.Empty);

                string handlerName = worker == null || string.IsNullOrWhiteSpace(worker.FullName)
                    ? handler.ToString()
                    : worker.FullName;
                GUI.Label(new Rect(left + 24f, top + 18f, width - 48f, 28f),
                    $"VEHICLE HANDLER — {handlerName}");
                GUI.Label(new Rect(left + 24f, top + 48f, width - 48f, 22f),
                    AssignmentSummary());

                float rowTop = top + 86f;
                DrawVehicleRow(left, rowTop, width);
                rowTop += RowHeight;
                DrawPropertyRow(left, rowTop, width);
                rowTop += RowHeight;
                DrawBayRow(left, rowTop, width);
                rowTop += RowHeight + 8f;

                if (draft != null)
                {
                    draft.Enabled = GUI.Toggle(
                        new Rect(left + 24f, rowTop, width - 48f, 28f),
                        draft.Enabled,
                        " Assignment enabled");
                }

                rowTop += 42f;
                float buttonWidth = (width - 64f) / 3f;
                if (GUI.Button(new Rect(left + 24f, rowTop, buttonWidth, 34f), "Apply assignment"))
                {
                    Apply();
                }

                if (GUI.Button(
                    new Rect(left + 32f + buttonWidth, rowTop, buttonWidth, 34f),
                    "Remove assignment"))
                {
                    Remove();
                }

                if (GUI.Button(
                    new Rect(left + 40f + (buttonWidth * 2f), rowTop, buttonWidth, 34f),
                    "Refresh choices"))
                {
                    Refresh(false);
                }

                rowTop += 48f;
                GUI.Label(new Rect(left + 24f, rowTop, width - 48f, 42f),
                    "Load and hide/release actions remain locked until the M4 movement and reservation safety layer is installed.");
                rowTop += 48f;
                GUI.Label(new Rect(left + 24f, rowTop, width - 48f, 58f),
                    string.IsNullOrEmpty(status) ? "Choose an owned vehicle, property, and loading bay." : status);
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        private static void DrawVehicleRow(float left, float top, float width)
        {
            GUI.Label(new Rect(left + 24f, top + 10f, 120f, 24f), "Owned vehicle");
            if (GUI.Button(new Rect(left + 150f, top + 5f, 36f, 30f), "<") && draft != null)
            {
                draft.CycleVehicle(-1);
            }

            GUI.Label(new Rect(left + 194f, top + 10f, width - 286f, 24f),
                draft?.SelectedVehicle?.DisplayName ?? "None available");
            if (GUI.Button(new Rect(left + width - 68f, top + 5f, 36f, 30f), ">") && draft != null)
            {
                draft.CycleVehicle(1);
            }
        }

        private static void DrawPropertyRow(float left, float top, float width)
        {
            GUI.Label(new Rect(left + 24f, top + 10f, 120f, 24f), "Destination");
            if (GUI.Button(new Rect(left + 150f, top + 5f, 36f, 30f), "<") && draft != null)
            {
                draft.CycleProperty(-1);
            }

            GUI.Label(new Rect(left + 194f, top + 10f, width - 286f, 24f),
                draft?.SelectedProperty?.DisplayName ?? "None available");
            if (GUI.Button(new Rect(left + width - 68f, top + 5f, 36f, 30f), ">") && draft != null)
            {
                draft.CycleProperty(1);
            }
        }

        private static void DrawBayRow(float left, float top, float width)
        {
            GUI.Label(new Rect(left + 24f, top + 10f, 120f, 24f), "Loading bay");
            if (GUI.Button(new Rect(left + 150f, top + 5f, 36f, 30f), "<") && draft != null)
            {
                draft.CycleBay(-1);
            }

            GUI.Label(new Rect(left + 194f, top + 10f, width - 286f, 24f),
                draft?.SelectedBay?.DisplayName ?? "None available");
            if (GUI.Button(new Rect(left + width - 68f, top + 5f, 36f, 30f), ">") && draft != null)
            {
                draft.CycleBay(1);
            }
        }

        private static string AssignmentSummary()
        {
            if (handler.IsEmpty ||
                !HandlerAssignmentRuntime.Registry.TryGet(handler, out HandlerAssignmentSnapshot assignment))
            {
                return "No assignment saved in this session.";
            }

            return $"Revision {assignment.Revision} • {assignment.TripState} • " +
                (assignment.Enabled ? "enabled" : "disabled");
        }

        private static void Apply()
        {
            if (!CanWrite())
            {
                return;
            }

            if (draft == null)
            {
                status = "Assignment choices are not available.";
                return;
            }

            VehicleId selectedVehicle = draft.SelectedVehicle?.Id ?? default;
            LoadingBayKey selectedDestination = draft.SelectedBay?.Key ?? default;
            bool enabled = draft.Enabled;
            if (!Refresh(true, selectedVehicle, selectedDestination, enabled))
            {
                return;
            }

            if (!draft.TryCreateConfiguration(
                out HandlerAssignmentConfiguration configuration,
                out string detail))
            {
                status = detail;
                return;
            }

            AssignmentWriteResult result = HandlerAssignmentRuntime.Registry.TryConfigure(
                configuration,
                draft.ExpectedRevision);
            HandlerAssignmentRuntime.ObserveRevision(handler, result.Revision);
            draft.ObserveRevision(result.Revision);
            status = Describe(result, "Assignment applied.");
        }

        private static void Remove()
        {
            if (!CanWrite())
            {
                return;
            }

            if (!HandlerAssignmentRuntime.Registry.TryGet(handler, out HandlerAssignmentSnapshot current))
            {
                status = "No assignment exists for this Vehicle Handler.";
                return;
            }

            AssignmentWriteResult result = HandlerAssignmentRuntime.Registry.TryRemove(
                handler,
                current.Revision);
            HandlerAssignmentRuntime.ObserveRevision(handler, result.Revision);
            if (result.Applied)
            {
                Refresh(false);
            }

            status = Describe(result, "Assignment removed.");
        }

        private static bool CanWrite()
        {
            if (worker == null || handler.IsEmpty)
            {
                status = "The selected Vehicle Handler is no longer available.";
                return false;
            }

            if (!worker.IsServerInitialized)
            {
                status = "Only the server/host can change Vehicle Handler assignments.";
                return false;
            }

            return true;
        }

        private static void Refresh(bool preserveSelection)
        {
            Refresh(preserveSelection, default, default, draft == null || draft.Enabled);
        }

        private static bool Refresh(
            bool preserveSelection,
            VehicleId selectedVehicle,
            LoadingBayKey selectedDestination,
            bool enabled)
        {
            if (handler.IsEmpty)
            {
                return false;
            }

            List<ClipboardVehicleChoice> vehicles = BuildVehicleChoices();
            List<ClipboardPropertyChoice> properties = BuildPropertyChoices();
            HandlerAssignmentRuntime.Registry.TryGet(handler, out HandlerAssignmentSnapshot current);
            long expectedRevision = HandlerAssignmentRuntime.GetExpectedRevision(handler, current);
            HandlerClipboardDraft replacement = new HandlerClipboardDraft(
                handler,
                vehicles,
                properties,
                current,
                expectedRevision);

            if (preserveSelection)
            {
                if (!selectedVehicle.IsEmpty && !replacement.TrySelectVehicle(selectedVehicle))
                {
                    status = "The selected vehicle is no longer owned or available. Refresh and choose again.";
                    draft = replacement;
                    return false;
                }

                if (!selectedDestination.IsEmpty &&
                    !replacement.TrySelectDestination(selectedDestination))
                {
                    status = "The selected destination bay is no longer available. Refresh and choose again.";
                    draft = replacement;
                    return false;
                }

                replacement.Enabled = enabled;
            }

            draft = replacement;
            status = ChoiceStatus(vehicles.Count, properties.Count);
            return true;
        }

        private static List<ClipboardVehicleChoice> BuildVehicleChoices()
        {
            List<ClipboardVehicleChoice> choices = new List<ClipboardVehicleChoice>();
            if (!VehicleManager.InstanceExists || VehicleManager.Instance == null)
            {
                return choices;
            }

            Il2CppSystem.Collections.Generic.List<LandVehicle> vehicles =
                VehicleManager.Instance.PlayerOwnedVehicles;
            if (vehicles == null)
            {
                return choices;
            }

            for (int index = 0; index < vehicles.Count; index++)
            {
                LandVehicle vehicle = vehicles[index];
                if (vehicle == null || !vehicle.IsPlayerOwned ||
                    !VehicleId.TryParse(vehicle.GUID.ToString(), out VehicleId id))
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(vehicle.VehicleName)
                    ? vehicle.VehicleCode
                    : vehicle.VehicleName;
                choices.Add(new ClipboardVehicleChoice(
                    id,
                    $"{name} ({ShortId(id.ToString())})"));
            }

            choices.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase));
            return choices;
        }

        private static List<ClipboardPropertyChoice> BuildPropertyChoices()
        {
            List<ClipboardPropertyChoice> choices = new List<ClipboardPropertyChoice>();
            Il2CppSystem.Collections.Generic.List<Property> properties = Property.OwnedProperties;
            if (properties == null)
            {
                return choices;
            }

            for (int propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
            {
                Property property = properties[propertyIndex];
                if (property == null || !property.IsOwned ||
                    string.IsNullOrWhiteSpace(property.PropertyCode) ||
                    property.LoadingDocks == null)
                {
                    continue;
                }

                PropertyKey propertyKey = new PropertyKey(property.PropertyCode);
                List<ClipboardBayChoice> bays = new List<ClipboardBayChoice>();
                for (int dockIndex = 0; dockIndex < property.LoadingDocks.Length; dockIndex++)
                {
                    LoadingDock dock = property.LoadingDocks[dockIndex];
                    if (dock == null || dock.ParentProperty != property ||
                        !DockId.TryParse(dock.GUID.ToString(), out DockId dockId))
                    {
                        continue;
                    }

                    LoadingBayKey key = new LoadingBayKey(propertyKey, dockId, dockIndex);
                    string dockName = string.IsNullOrWhiteSpace(dock.Name)
                        ? $"Loading bay {dockIndex + 1}"
                        : dock.Name;
                    bays.Add(new ClipboardBayChoice(key, $"{dockName} (bay {dockIndex + 1})"));
                }

                if (bays.Count == 0)
                {
                    continue;
                }

                string propertyName = string.IsNullOrWhiteSpace(property.PropertyName)
                    ? property.PropertyCode
                    : property.PropertyName;
                choices.Add(new ClipboardPropertyChoice(propertyKey, propertyName, bays));
            }

            choices.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase));
            return choices;
        }

        private static string ChoiceStatus(int vehicleCount, int propertyCount)
        {
            if (vehicleCount == 0)
            {
                return "No player-owned vehicles are currently available.";
            }

            if (propertyCount == 0)
            {
                return "No owned property with a loading bay is currently available.";
            }

            return "Choose an owned vehicle, property, and loading bay, then apply the assignment.";
        }

        private static string Describe(AssignmentWriteResult result, string success)
        {
            if (result.Applied)
            {
                return $"{success} Revision {result.Revision}.";
            }

            if (!string.IsNullOrWhiteSpace(result.Detail))
            {
                return $"{result.Status}: {result.Detail}";
            }

            return result.Status.ToString();
        }

        private static string ShortId(string value) =>
            string.IsNullOrEmpty(value) || value.Length <= 8 ? value : value.Substring(0, 8);
    }

    [HarmonyPatch(
        typeof(ManagementInterface),
        nameof(ManagementInterface.Open),
        typeof(Il2CppSystem.Collections.Generic.List<IConfigurable>),
        typeof(ManagementClipboard_Equippable))]
    internal static class HandlerManagementOpenPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Il2CppSystem.Collections.Generic.List<IConfigurable> configurables)
        {
            HandlerClipboardSession.Begin(configurables);
        }
    }

    [HarmonyPatch(typeof(ManagementInterface), nameof(ManagementInterface.InitializeConfigPanel))]
    internal static class HandlerManagementPanelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ManagementInterface __instance)
        {
            HandlerClipboardSession.HideVanillaPanel(__instance);
        }
    }

    [HarmonyPatch(typeof(ManagementInterface), nameof(ManagementInterface.Close), typeof(bool))]
    internal static class HandlerManagementClosePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            HandlerClipboardSession.End();
        }
    }
}
