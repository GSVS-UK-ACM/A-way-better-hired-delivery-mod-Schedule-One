using System;
using Il2CppInterop.Runtime.Injection;
using VehicleHandlers.Employees;

namespace VehicleHandlers.Runtime
{
    internal static class HandlerRuntimeRegistration
    {
        private static bool registered;

        public static void Register()
        {
            if (registered)
            {
                return;
            }

            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<HandlerEmployee>())
            {
                ClassInjector.RegisterTypeInIl2Cpp<HandlerEmployee>();
            }

            registered = ClassInjector.IsTypeRegisteredInIl2Cpp<HandlerEmployee>();
            if (!registered)
            {
                throw new InvalidOperationException("HandlerEmployee was not registered with the IL2CPP runtime.");
            }
        }
    }
}
