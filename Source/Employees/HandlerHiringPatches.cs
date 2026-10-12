using System;
using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Property;
using MelonLoader;
using UnityEngine;

namespace VehicleHandlers.Employees
{
    internal static class HandlerAttachment
    {
        public static void TryAttach(Employee worker, string source)
        {
            if (worker == null)
            {
                MelonLogger.Warning($"Handler marker attachment after {source} received no employee; preserving the vanilla hiring result.");
                return;
            }

            try
            {
                if (worker.GetComponent<HandlerEmployee>() != null)
                {
                    return;
                }

                HandlerEmployee marker = HandlerEmployee.EnsureAttached(worker);
                if (marker == null)
                {
                    MelonLogger.Warning($"Handler marker attachment after {source} failed; preserving the vanilla employee.");
                    return;
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
        [HarmonyPostfix]
        private static void Postfix(EEmployeeType type, Employee __result)
        {
            if (type == EEmployeeType.Handler)
            {
                HandlerAttachment.TryAttach(__result, nameof(EmployeeManager.CreateEmployee_Server));
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
        private static void Postfix(Employee __instance)
        {
            if (__instance != null && __instance.EmployeeType == EEmployeeType.Handler)
            {
                HandlerAttachment.TryAttach(__instance, nameof(Employee.Initialize));
            }
        }
    }
}
