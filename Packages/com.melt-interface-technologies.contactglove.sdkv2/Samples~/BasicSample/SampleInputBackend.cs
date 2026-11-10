using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    /// <summary>Uses the enabled input backend without requiring the optional Input System package.</summary>
    public static class SampleInputBackend
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static readonly Type KeyboardType=Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
        private static readonly PropertyInfo Current=KeyboardType?.GetProperty("current",BindingFlags.Public|BindingFlags.Static);
        private static readonly PropertyInfo F=KeyboardType?.GetProperty("fKey");
        private static readonly PropertyInfo J=KeyboardType?.GetProperty("jKey");
        private static readonly PropertyInfo Pressed=Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem")?.GetProperty("wasPressedThisFrame");
#endif
        public static bool WasPressed(bool left)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(left?KeyCode.F:KeyCode.J);
#elif ENABLE_INPUT_SYSTEM
            var keyboard=Current?.GetValue(null);if(keyboard==null)return false;
            var button=(left?F:J)?.GetValue(keyboard);
            return button!=null && Pressed?.GetValue(button) is bool pressed && pressed;
#else
            return false;
#endif
        }
        public static void ConfigureEventSystems()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var moduleType=Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            foreach(var events in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                foreach(var legacy in events.GetComponents<StandaloneInputModule>())legacy.enabled=false;
                if(moduleType==null){Debug.LogWarning("ContactGlove sample requires InputSystemUIInputModule for the enabled Input System backend.");continue;}
                // Retain user-provided action bindings. New modules assign their standard UI actions on enable.
                if(events.GetComponent(moduleType)==null)events.gameObject.AddComponent(moduleType);
            }
#endif
        }
    }
}
