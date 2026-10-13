using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2CppScheduleOne.Persistence;
using MelonLoader;
using VehicleHandlers.Assignments;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Persistence
{
    internal static class HandlerAssignmentPersistenceRuntime
    {
        private static string activeSaveRoot = string.Empty;

        public static void PrepareLoad(string saveRoot)
        {
            HandlerAssignmentRuntime.BeginRestore();
            activeSaveRoot = NormalizeRoot(saveRoot);
            if (string.IsNullOrWhiteSpace(activeSaveRoot))
            {
                MelonLogger.Warning("Vehicle Handler assignments were reset, but no save path was available to restore.");
                return;
            }

            string path = AssignmentPersistenceFile.BuildPath(activeSaveRoot);
            try
            {
                if (!AssignmentPersistenceFile.TryLoad(
                    path,
                    out IReadOnlyList<HandlerAssignmentSnapshot> assignments,
                    out bool recoveredBackup,
                    out string detail))
                {
                    MelonLogger.Error($"Vehicle Handler assignments were not restored: {detail}");
                    return;
                }

                int restored = 0;
                for (int index = 0; index < assignments.Count; index++)
                {
                    HandlerAssignmentSnapshot assignment = assignments[index];
                    AssignmentWriteResult result = HandlerAssignmentRuntime.Registry.TryRestore(assignment);
                    if (result.Applied)
                    {
                        HandlerAssignmentRuntime.ObserveRevision(assignment.Handler, result.Revision);
                        restored++;
                    }
                    else
                    {
                        MelonLogger.Warning(
                            $"Skipped saved Vehicle Handler assignment for {assignment.Handler}: " +
                            $"{result.Status} {result.Detail}");
                    }
                }

                string recovery = recoveredBackup ? " from backup" : string.Empty;
                MelonLogger.Msg($"Restored {restored} Vehicle Handler assignment(s){recovery}.");
            }
            catch (Exception exception)
            {
                MelonLogger.Error($"Vehicle Handler assignment restore failed safely: {exception.Message}");
            }
        }

        public static void SaveActive(string saveRootHint = null)
        {
            string root = ResolveActiveSaveRoot(saveRootHint);
            if (string.IsNullOrWhiteSpace(root))
            {
                MelonLogger.Warning("Vehicle Handler assignments were not saved because no active save path was available.");
                return;
            }

            activeSaveRoot = root;
            string path = AssignmentPersistenceFile.BuildPath(root);
            try
            {
                IReadOnlyList<HandlerAssignmentSnapshot> assignments =
                    HandlerAssignmentRuntime.Registry.Snapshot();
                AssignmentPersistenceFile.Save(path, assignments, out bool changed);
                if (changed)
                {
                    MelonLogger.Msg($"Saved {assignments.Count} Vehicle Handler assignment(s).");
                }
            }
            catch (Exception exception)
            {
                MelonLogger.Error($"Vehicle Handler assignment save failed safely: {exception.Message}");
            }
        }

        public static void ClearActiveSave()
        {
            activeSaveRoot = string.Empty;
            HandlerAssignmentRuntime.BeginRestore();
        }

        private static string ResolveActiveSaveRoot(string hint)
        {
            string normalizedHint = NormalizeRoot(hint);
            if (!string.IsNullOrWhiteSpace(normalizedHint))
            {
                return normalizedHint;
            }

            if (!string.IsNullOrWhiteSpace(activeSaveRoot))
            {
                return activeSaveRoot;
            }

            if (LoadManager.InstanceExists && LoadManager.Instance != null)
            {
                string loaded = NormalizeRoot(LoadManager.Instance.LoadedGameFolderPath);
                if (!string.IsNullOrWhiteSpace(loaded))
                {
                    return loaded;
                }

                SaveInfo active = LoadManager.Instance.ActiveSaveInfo;
                string activePath = NormalizeRoot(active?.SavePath);
                if (!string.IsNullOrWhiteSpace(activePath))
                {
                    return activePath;
                }
            }

            if (SaveManager.InstanceExists && SaveManager.Instance != null)
            {
                string players = NormalizeRoot(SaveManager.Instance.PlayersSavePath);
                if (!string.IsNullOrWhiteSpace(players) &&
                    string.Equals(Path.GetFileName(players), "Players", StringComparison.OrdinalIgnoreCase))
                {
                    return Directory.GetParent(players)?.FullName ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string NormalizeRoot(string path) =>
            string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)
                ? string.Empty
                : Path.GetFullPath(path);
    }

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.StartGame), new Type[]
    {
        typeof(SaveInfo), typeof(bool), typeof(bool)
    })]
    internal static class HandlerAssignmentLoadPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SaveInfo __0)
        {
            HandlerAssignmentPersistenceRuntime.PrepareLoad(__0?.SavePath);
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), new Type[0])]
    internal static class HandlerAssignmentSavePatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            HandlerAssignmentPersistenceRuntime.SaveActive();
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), new Type[] { typeof(string) })]
    internal static class HandlerAssignmentNamedSavePatch
    {
        [HarmonyPrefix]
        public static void Prefix(string __0)
        {
            HandlerAssignmentPersistenceRuntime.SaveActive(__0);
        }
    }

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.CleanUp), new Type[0])]
    internal static class HandlerAssignmentLoadCleanupPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            HandlerAssignmentPersistenceRuntime.ClearActiveSave();
        }
    }
}
