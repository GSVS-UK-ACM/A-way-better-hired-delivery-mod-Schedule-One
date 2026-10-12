using System;
using HarmonyLib;
using Il2CppScheduleOne.Employees;
using MelonLoader;

namespace VehicleHandlers.Employees
{
    [HarmonyPatch(typeof(Employee), nameof(Employee.InitializeAppearance), typeof(bool), typeof(int))]
    internal static class HandlerAppearancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Employee __instance)
        {
            if (__instance == null || __instance.GetComponent<HandlerEmployee>() == null)
            {
                return;
            }

            try
            {
                if (!HandlerAppearance.TryApply(__instance.Avatar))
                {
                    MelonLogger.Warning("Handler appearance could not find a colorable worn object; preserving the generated employee appearance.");
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Warning($"Handler appearance override failed safely: {exception.Message}");
            }
        }
    }
}
