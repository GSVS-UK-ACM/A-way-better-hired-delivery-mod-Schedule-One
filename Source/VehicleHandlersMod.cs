using MelonLoader;
using VehicleHandlers.Runtime;

namespace VehicleHandlers
{
    public sealed class VehicleHandlersMod : MelonMod
    {
        public const string ModName = "Vehicle Handlers";
        public const string Version = "0.1.0";
        public const string TargetGameVersion = "0.4.7f12";
        public const string BuildIdentity = "0.4.7f12-dev2";
        public const string Creator = "GSVS UK ACM";
        public const string ProjectOwner = "xboxnuker-rgb";
        public const string ModDescription = "Adds a hireable Handler who moves owned vehicles while respecting loading-bay reservations.";

        public override void OnInitializeMelon()
        {
            HandlerRuntimeRegistration.Register();
            StartupBanner.Print();
            MelonLogger.Msg($"{ModName} {Version} initialized. HandlerEmployee is registered for IL2CPP runtime construction.");
        }
    }
}

