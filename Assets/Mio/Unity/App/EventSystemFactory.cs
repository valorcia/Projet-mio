using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mio.Unity.App
{
    /// <summary>
    /// Creates an EventSystem with whichever input module this project's
    /// backend actually supports.
    ///
    /// A Unity 6 project may be on the new Input System, the legacy manager, or
    /// both. Adding StandaloneInputModule under the new backend throws at
    /// runtime, so the module is chosen by probing for the package instead of
    /// by a compile-time symbol. That keeps the project free of an Input System
    /// assembly reference it might not have.
    /// </summary>
    public static class EventSystemFactory
    {
        private const string InputSystemModule =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        public static void EnsureExists()
        {
            if (EventSystem.current != null) return;
            Create();
        }

        /// <summary>
        /// Builds the EventSystem object and returns it.
        ///
        /// Public so the editor setup tool can place one in a generated scene:
        /// a tester who opens the scene should be able to see that input is
        /// wired, rather than having to take a runtime factory on trust. The
        /// runtime call then finds it already there and does nothing.
        /// </summary>
        public static GameObject Create()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));

            var moduleType = Type.GetType(InputSystemModule);
            if (moduleType != null)
            {
                go.AddComponent(moduleType);
                return go;
            }

            go.AddComponent<StandaloneInputModule>();
            return go;
        }
    }
}
