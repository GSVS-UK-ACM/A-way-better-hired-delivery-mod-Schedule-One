using MelonLoader;

namespace VehicleHandlers
{
    internal static class StartupBanner
    {
        private const string Reset = "\u001b[0m";
        private const string White = "\u001b[97m";
        private const string Green = "\u001b[1;38;5;46m";

        // Orange shades match the Handler's high-visibility role color.
        private const string Orange1 = "\u001b[1;38;5;214m";
        private const string Orange2 = "\u001b[1;38;5;208m";
        private const string Orange3 = "\u001b[38;5;202m";
        private const string Orange4 = "\u001b[38;5;166m";
        private const string Orange5 = "\u001b[38;5;130m";
        private const string Orange6 = "\u001b[38;5;94m";

        public static void Print()
        {
            MelonLogger.Msg(string.Empty);

            MelonLogger.Msg(
                $"{Orange6}──────────────{Reset} " +
                $"{Green}CREATED BY{Reset} " +
                $"{Orange6}──────────────{Reset}");

            PrintRow(
                Orange1,
                "  ██████╗  ███████╗ ██╗   ██╗ ███████╗  ",
                "Vehicle Handlers ",
                $"v{VehicleHandlersMod.Version}");

            PrintRow(
                Orange2,
                " ██╔════╝  ██╔════╝ ██║   ██║ ██╔════╝  ",
                "Schedule I ",
                $"{VehicleHandlersMod.TargetGameVersion} | IL2CPP");

            PrintRow(
                Orange3,
                " ██║  ███╗ ███████╗ ██║   ██║ ███████╗  ",
                "Build: ",
                VehicleHandlersMod.BuildIdentity);

            PrintRow(
                Orange4,
                " ██║   ██║ ╚════██║ ╚██╗ ██╔╝ ╚════██║  ",
                "Created by ",
                VehicleHandlersMod.Creator);

            PrintRow(
                Orange5,
                " ╚██████╔╝ ███████║  ╚████╔╝  ███████║  ",
                "GitHub: ",
                VehicleHandlersMod.ProjectOwner);

            MelonLogger.Msg(
                $"{Orange6}  ╚═════╝  ╚══════╝   ╚═══╝   ╚══════╝  {Reset}  " +
                $"{White}Hire. Assign. Deliver.{Reset}");

            MelonLogger.Msg(
                $"{Orange6}────────────────────────────────────────────{Reset}" +
                $"{Green}◆{Reset}" +
                $"{Orange6}────────────────────────{Reset}");
        }

        private static void PrintRow(
            string artColor,
            string art,
            string label,
            string value)
        {
            MelonLogger.Msg(
                $"{artColor}{art}{Reset}  " +
                $"{White}{label}{Reset}" +
                $"{Green}{value}{Reset}");
        }
    }
}
