using UnityEngine;

namespace SceneContent.Unity
{
    internal static class SceneComponents
    {
        public static void Attach(SceneDocument document, Transform[] nodes, bool activateCameras)
        {
            foreach (SceneCamera binding in document.Cameras)
            {
                Camera camera = nodes[binding.Node].gameObject.AddComponent<Camera>();
                camera.orthographic = binding.Projection == "orthographic";
                camera.fieldOfView = binding.FieldOfView;
                camera.orthographicSize = binding.OrthographicSize;
                camera.nearClipPlane = binding.Near;
                camera.farClipPlane = binding.Far;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(binding.ClearColor[0], binding.ClearColor[1], binding.ClearColor[2], binding.ClearColor[3]);
                camera.enabled = activateCameras && binding.Enabled;
            }
            foreach (SceneLight binding in document.Lights)
            {
                Light light = nodes[binding.Node].gameObject.AddComponent<Light>();
                light.type = binding.Kind == "directional" ? LightType.Directional : (binding.Kind == "point" ? LightType.Point : LightType.Spot);
                light.color = new Color(binding.Color[0], binding.Color[1], binding.Color[2]);
                light.intensity = binding.Intensity;
                light.range = binding.Range;
                light.spotAngle = binding.SpotAngle;
                light.shadows = binding.Shadows ? LightShadows.Soft : LightShadows.None;
                light.enabled = binding.Enabled;
            }
        }
    }
}
