using System;
using HarmonyLib;
using Il2CppScheduleOne.Delivery;
using Il2CppScheduleOne.Property;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Reservations
{
    internal static class HandlerReservationRuntime
    {
        private static HandlerReservationRegistry registry = new HandlerReservationRegistry();

        public static IHandlerReservationRegistry Registry => registry;

        public static void Reset()
        {
            registry = new HandlerReservationRegistry();
        }

        public static bool IsReserved(Property property, int dockIndex)
        {
            return TryResolveBay(property, dockIndex, out LoadingBayKey bay) &&
                registry.IsReserved(bay);
        }

        internal static bool TryResolveBay(
            Property property,
            int dockIndex,
            out LoadingBayKey bay)
        {
            bay = default;
            if (property == null ||
                string.IsNullOrWhiteSpace(property.PropertyCode) ||
                property.LoadingDocks == null ||
                dockIndex < 0 ||
                dockIndex >= property.LoadingDocks.Length)
            {
                return false;
            }

            LoadingDock dock = property.LoadingDocks[dockIndex];
            if (dock == null ||
                dock.ParentProperty != property ||
                !DockId.TryParse(dock.GUID.ToString(), out DockId dockId))
            {
                return false;
            }

            bay = new LoadingBayKey(
                new PropertyKey(property.PropertyCode),
                dockId,
                dockIndex);
            return true;
        }
    }

    [HarmonyPatch(
        typeof(DeliveryManager),
        nameof(DeliveryManager.IsLoadingBayFree),
        new Type[] { typeof(Property), typeof(int) })]
    internal static class HandlerLoadingBayAvailabilityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Property __0, int __1, ref bool __result)
        {
            if (__result && HandlerReservationRuntime.IsReserved(__0, __1))
            {
                __result = false;
            }
        }
    }
}
