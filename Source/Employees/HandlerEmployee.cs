using System;
using Il2CppScheduleOne.Employees;
using MelonLoader;
using UnityEngine;

namespace VehicleHandlers.Employees
{
    public sealed class HandlerEmployee : MonoBehaviour
    {
        public HandlerEmployee(IntPtr nativePointer)
            : base(nativePointer)
        {
        }

        public void Awake()
        {
            Employee worker = GetComponent<Employee>();
            if (worker == null)
            {
                MelonLogger.Error("HandlerEmployee requires a game Employee component on the same object.");
                enabled = false;
                return;
            }

            worker.Type = EEmployeeType.Handler;
        }
    }
}
