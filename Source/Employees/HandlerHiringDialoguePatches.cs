using System;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.Dialogue;
using Il2CppSystem.Collections.Generic;
using MelonLoader;

namespace VehicleHandlers.Employees
{
    [HarmonyPatch]
    internal static class HandlerHiringChoiceListPatch
    {
        private const string VanillaPackagerLabel = "Packager";
        private static bool logged;

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(DialogueController_Fixer),
                nameof(DialogueController_Fixer.ModifyChoiceList),
                new[]
                {
                    typeof(string),
                    typeof(List<DialogueChoiceData>).MakeByRefType()
                });
        }

        [HarmonyPostfix]
        private static void Postfix(ref List<DialogueChoiceData> existingChoices)
        {
            if (existingChoices == null)
            {
                return;
            }

            DialogueChoiceData packagerChoice = null;

            for (int index = 0; index < existingChoices.Count; index++)
            {
                DialogueChoiceData choice = existingChoices[index];
                if (choice == null)
                {
                    continue;
                }

                if (choice.ChoiceLabel == VehicleHandlerIdentity.ChoiceLabel)
                {
                    return;
                }

                if (choice.ChoiceLabel == VanillaPackagerLabel)
                {
                    packagerChoice = choice;
                }
            }

            if (packagerChoice == null)
            {
                return;
            }

            DialogueChoiceData vehicleHandlerChoice = packagerChoice.GetCopy();
            vehicleHandlerChoice.ChoiceLabel = VehicleHandlerIdentity.ChoiceLabel;
            vehicleHandlerChoice.ChoiceText = VehicleHandlerIdentity.ChoiceText;
            existingChoices.Add(vehicleHandlerChoice);

            if (!logged)
            {
                MelonLogger.Msg("Added the distinct Vehicle Handler option to the fixer hiring dialogue.");
                logged = true;
            }
        }
    }

    [HarmonyPatch(
        typeof(DialogueController_Fixer),
        nameof(DialogueController_Fixer.ChoiceCallback),
        typeof(string))]
    internal static class HandlerHiringChoiceCallbackPatch
    {
        private const string VanillaPackagerLabel = "Packager";

        [HarmonyPrefix]
        private static void Prefix(ref string choiceLabel, out bool __state)
        {
            __state = choiceLabel == VehicleHandlerIdentity.ChoiceLabel;
            if (__state)
            {
                choiceLabel = VanillaPackagerLabel;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(DialogueController_Fixer __instance, bool __state)
        {
            if (__state)
            {
                __instance.selectedEmployeeType = VehicleHandlerIdentity.CustomEmployeeType;
            }
        }
    }
}
