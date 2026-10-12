using System;
using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Property;
using MelonLoader;
using UnityEngine;

namespace VehicleHandlers.Employees
{
    internal static class VehicleHandlerIdentity
    {
        public const string ChoiceLabel = "VehicleHandler";
        public const string ChoiceText = "Vehicle Handler (moving owned vehicles)";
        public const string EmployeeIdPrefix = "vehiclehandler_";
        public const EEmployeeType CustomEmployeeType = (EEmployeeType)4;

        public static bool IsMarkedEmployeeId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith(EmployeeIdPrefix, StringComparison.Ordinal);
        }

        public static string MarkEmployeeId(string id)
        {
            if (IsMarkedEmployeeId(id))
            {
                return id;
            }

            return EmployeeIdPrefix + id;
        }
    }

    internal static class HandlerAttachment
    {
        public static void TryAttach(Employee worker, string source, string guid = null)
        {
            if (worker == null)
            {
                MelonLogger.Warning($"Handler marker attachment after {source} received no employee; preserving the vanilla hiring result.");
                return;
            }

            try
            {
                HandlerEmployee existing = worker.GetComponent<HandlerEmployee>();
                if (existing != null)
                {
                    existing.BindIdentity(guid);
                    return;
                }

                HandlerEmployee marker = HandlerEmployee.EnsureAttached(worker);
                if (marker == null)
                {
                    MelonLogger.Warning($"Handler marker attachment after {source} failed; preserving the vanilla employee.");
                    return;
                }

                marker.BindIdentity(guid);

                if (!HandlerAppearance.TryApply(worker.Avatar))
                {
                    MelonLogger.Warning("Vehicle Handler appearance could not find a colorable worn object; preserving the generated employee appearance.");
                }

                MelonLogger.Msg($"Attached HandlerEmployee after {source} to {worker.GetType().FullName}.");
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"Handler marker attachment after {source} failed safely: {exception.Message}");
            }
        }
    }

    [HarmonyPatch(
        typeof(EmployeeManager),
        nameof(EmployeeManager.CreateEmployee_Server),
        typeof(Property),
        typeof(EEmployeeType),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(bool),
        typeof(int),
        typeof(Vector3),
        typeof(Quaternion),
        typeof(string))]
    internal static class HandlerCreateEmployeeServerPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref EEmployeeType type, ref string id, out bool __state)
        {
            __state = type == VehicleHandlerIdentity.CustomEmployeeType || VehicleHandlerIdentity.IsMarkedEmployeeId(id);

            if (type == VehicleHandlerIdentity.CustomEmployeeType)
            {
                type = EEmployeeType.Handler;
                id = VehicleHandlerIdentity.MarkEmployeeId(id);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(bool __state, Employee __result, string guid)
        {
            if (__state)
            {
                HandlerAttachment.TryAttach(__result, nameof(EmployeeManager.CreateEmployee_Server), guid);
            }
        }
    }

    [HarmonyPatch(
        typeof(Employee),
        nameof(Employee.Initialize),
        typeof(NetworkConnection),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(bool),
        typeof(int))]
    internal static class HandlerInitializePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Employee __instance, string id, string guid)
        {
            if (VehicleHandlerIdentity.IsMarkedEmployeeId(id))
            {
                HandlerAttachment.TryAttach(__instance, nameof(Employee.Initialize), guid);
            }
        }
    }

    [HarmonyPatch(
        typeof(EmployeeManager),
        nameof(EmployeeManager.GetEmployeePrefab),
        typeof(EEmployeeType))]
    internal static class VehicleHandlerPrefabLookupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(EmployeeManager __instance, EEmployeeType type, ref Employee __result)
        {
            if (type != VehicleHandlerIdentity.CustomEmployeeType)
            {
                return true;
            }

            __result = __instance.PackagerPrefab;
            return false;
        }
    }
}
