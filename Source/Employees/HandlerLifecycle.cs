using System;
using HarmonyLib;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ObjectScripts;
using MelonLoader;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Employees
{
    internal static class HandlerLifecycleBridge
    {
        private static IHandlerLifecycleSink sink;
        private static bool unloadingNotified;

        internal static void SetSink(IHandlerLifecycleSink value)
        {
            sink = value;
            unloadingNotified = false;
        }

        internal static void NotifyUnavailable(HandlerId handler, HandlerUnavailableReason reason)
        {
            if (sink == null)
            {
                return;
            }

            try
            {
                sink.HandlerUnavailable(handler, reason);
            }
            catch (Exception exception)
            {
                MelonLogger.Error($"Vehicle Handler lifecycle cleanup failed safely for {handler} ({reason}): {exception.Message}");
            }
        }

        internal static void NotifyModUnloading()
        {
            if (unloadingNotified)
            {
                return;
            }

            unloadingNotified = true;
            IHandlerLifecycleSink current = sink;
            sink = null;
            MelonLogger.Msg("Vehicle Handler lifecycle bridge shut down.");
            if (current == null)
            {
                return;
            }

            try
            {
                current.ModUnloading();
            }
            catch (Exception exception)
            {
                MelonLogger.Error($"Vehicle Handler mod-unload cleanup failed safely: {exception.Message}");
            }
        }
    }

    internal static class HandlerLifecycleFilter
    {
        internal static bool TryGetMarker(Employee worker, out HandlerEmployee marker)
        {
            marker = worker == null ? null : worker.GetComponent<HandlerEmployee>();
            return marker != null;
        }

        internal static bool BlockVanillaWork(Packager worker)
        {
            if (!TryGetMarker(worker, out HandlerEmployee marker))
            {
                return false;
            }

            marker.ReportVanillaWorkBlocked();
            return true;
        }
    }

    [HarmonyPatch(typeof(Packager), "ShouldIdle", new Type[] { })]
    internal static class HandlerShouldIdlePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Packager __instance, ref bool __result)
        {
            if (!HandlerLifecycleFilter.BlockVanillaWork(__instance))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Packager), nameof(Packager.StartPackaging), typeof(PackagingStation))]
    internal static class HandlerStartPackagingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Packager __instance) =>
            !HandlerLifecycleFilter.BlockVanillaWork(__instance);
    }

    [HarmonyPatch(typeof(Packager), nameof(Packager.StartPress), typeof(BrickPress))]
    internal static class HandlerStartPressPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Packager __instance) =>
            !HandlerLifecycleFilter.BlockVanillaWork(__instance);
    }

    [HarmonyPatch(typeof(Packager), nameof(Packager.StartMoveItem), typeof(PackagingStation))]
    internal static class HandlerStartMovePackagingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Packager __instance) =>
            !HandlerLifecycleFilter.BlockVanillaWork(__instance);
    }

    [HarmonyPatch(typeof(Packager), nameof(Packager.StartMoveItem), typeof(BrickPress))]
    internal static class HandlerStartMovePressPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Packager __instance) =>
            !HandlerLifecycleFilter.BlockVanillaWork(__instance);
    }

    [HarmonyPatch(typeof(Packager), "Fire", new Type[] { })]
    internal static class HandlerFirePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Packager __instance)
        {
            if (HandlerLifecycleFilter.TryGetMarker(__instance, out HandlerEmployee marker))
            {
                marker.NotifyUnavailable(HandlerUnavailableReason.Fired);
            }
        }
    }

    [HarmonyPatch(typeof(Employee), "LeavePropertyAndDespawn", new Type[] { })]
    internal static class HandlerLeaveAndDespawnPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Employee __instance)
        {
            if (HandlerLifecycleFilter.TryGetMarker(__instance, out HandlerEmployee marker))
            {
                marker.NotifyUnavailable(HandlerUnavailableReason.Despawned);
            }
        }
    }
}
