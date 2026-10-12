using System;
using Il2CppScheduleOne.Employees;
using MelonLoader;
using UnityEngine;
using VehicleHandlers.Contracts;

namespace VehicleHandlers.Employees
{
    public sealed class HandlerEmployee : MonoBehaviour
    {
        private Employee worker;
        private string handlerGuid;
        private bool unavailableNotified;

        public HandlerEmployee(IntPtr nativePointer)
            : base(nativePointer)
        {
        }

        public static HandlerEmployee EnsureAttached(Employee worker)
        {
            if (worker == null)
            {
                return null;
            }

            HandlerEmployee existing = worker.GetComponent<HandlerEmployee>();
            if (existing != null)
            {
                return existing;
            }

            return worker.gameObject.AddComponent<HandlerEmployee>();
        }

        public void Awake()
        {
            worker = GetComponent<Employee>();
            if (worker == null)
            {
                MelonLogger.Error("HandlerEmployee requires a game Employee component on the same object.");
                enabled = false;
                return;
            }

            worker.Type = EEmployeeType.Handler;
        }

        internal bool IsBaseWorkAvailable =>
            worker != null &&
            !unavailableNotified &&
            !worker.Fired &&
            worker.PaidForToday &&
            worker.AssignedProperty != null;

        internal void BindIdentity(string guid)
        {
            if (!string.IsNullOrWhiteSpace(guid))
            {
                handlerGuid = guid;
            }
        }

        internal void NotifyUnavailable(HandlerUnavailableReason reason)
        {
            if (unavailableNotified)
            {
                return;
            }

            unavailableNotified = true;
            string guid = handlerGuid;
            if (string.IsNullOrWhiteSpace(guid) && worker != null)
            {
                guid = worker.GUID.ToString();
            }

            if (!HandlerId.TryParse(guid, out HandlerId handler))
            {
                MelonLogger.Warning($"Vehicle Handler lifecycle event '{reason}' has no valid Handler GUID; cleanup will rely on load recovery.");
                return;
            }

            HandlerLifecycleBridge.NotifyUnavailable(handler, reason);
        }
    }
}
