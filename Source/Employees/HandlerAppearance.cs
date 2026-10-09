using Il2CppScheduleOne.Avatar;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.Core.Avatar;
using Il2CppScheduleOne.Core.Avatar.Properties;
using UnityEngine;

namespace VehicleHandlers.Employees
{
    internal static class HandlerAppearance
    {
        private static readonly Color UniformOrange = new Color(1.0f, 0.36f, 0.04f, 1.0f);

        public static bool TryApply(Avatar avatar)
        {
            if (avatar == null || avatar.Appearance == null)
            {
                return false;
            }

            AvatarAppearance appearance = avatar.Appearance;
            Il2CppSystem.Collections.Generic.List<AvatarObject> wornObjects = appearance._wornObjects;
            if (wornObjects == null)
            {
                return false;
            }

            for (int index = wornObjects.Count - 1; index >= 0; index--)
            {
                AvatarObject wornObject = wornObjects[index];
                if (wornObject == null || wornObject.PropertyCollection == null)
                {
                    continue;
                }

                ColorProperty colorProperty = wornObject.PropertyCollection.GetFirstColorProperty();
                if (colorProperty == null)
                {
                    continue;
                }

                colorProperty.SetValue(UniformOrange);
                appearance.RepaintLayers();
                return true;
            }

            return false;
        }
    }
}
