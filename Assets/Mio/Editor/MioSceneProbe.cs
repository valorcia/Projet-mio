using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mio.Editor
{
    /// <summary>
    /// Finds components inside a specific scene.
    ///
    /// Object.FindObjectOfType searches every loaded scene, which would let a
    /// component in the editor's currently open scene masquerade as one in the
    /// scene being validated — and report a broken scene as healthy. Walking
    /// the scene's own roots is the only way to be sure.
    /// </summary>
    public static class MioSceneProbe
    {
        public static T FindComponent<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        public static bool Has<T>(Scene scene) where T : Component
        {
            return FindComponent<T>(scene) != null;
        }
    }
}
