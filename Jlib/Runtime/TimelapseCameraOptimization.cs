using HarmonyLib;
using UnityEngine;

namespace Jlib.Runtime
{
    /// <summary>Remove the automatic draw from ONI's timelapse script host camera.</summary>
    public static class TimelapseCameraOptimization
    {
        public static void Apply(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(CameraSetupPatch)).Patch();
        }

        [HarmonyPatch(typeof(CameraController), "OnPrefabInit")]
        private static class CameraSetupPatch
        {
            private static void Postfix()
            {
                // Timelapser.RenderForTimelapser explicitly renders the base
                // and overlay cameras. The host's Camera is not used for that
                // capture; disabling just the Camera keeps Update/coroutines.
                var timelapser = Game.Instance != null ? Game.Instance.timelapser : null;
                var camera = timelapser != null ? timelapser.GetComponent<Camera>() : null;
                if (camera == null || camera.name != "timelapseCamera" || !camera.enabled)
                    return;
                camera.enabled = false;
                Debug.Log("[Jlib] Disabled redundant timelapse host camera draw");
            }
        }
    }
}
