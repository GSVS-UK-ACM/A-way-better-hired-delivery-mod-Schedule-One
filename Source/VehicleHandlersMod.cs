using MelonLoader;
using VehicleHandlers.Assignments;
using VehicleHandlers.Clipboard;
using VehicleHandlers.Employees;
using VehicleHandlers.Runtime;

namespace VehicleHandlers
{
    public sealed class VehicleHandlersMod : MelonMod
    {
        public const string ModName = "Vehicle Handlers";
        public const string Version = "0.1.0";
        public const string TargetGameVersion = "0.4.7f12";
        public const string BuildIdentity = "0.4.7f12-dev9";
        public const string Creator = "GSVS UK ACM";
        public const string ProjectOwner = "xboxnuker-rgb";
        public const string ModDescription = "Adds a hireable Handler who moves owned vehicles while respecting loading-bay reservations.";

        public override void OnInitializeMelon()
        {
            HandlerRuntimeRegistration.Register();
            HandlerAssignmentRuntime.Initialize();
            StartupBanner.Print();
            MelonLogger.Msg($"{ModName} {Version} initialized. Handler runtime, clipboard controls, and assignment persistence are ready.");
        }

        public override void OnGUI()
        {
            HandlerClipboardSession.Draw();
        }

        public override void OnDeinitializeMelon()
        {
            HandlerClipboardSession.End();
            HandlerLifecycleBridge.NotifyModUnloading();
        }
    }
}

