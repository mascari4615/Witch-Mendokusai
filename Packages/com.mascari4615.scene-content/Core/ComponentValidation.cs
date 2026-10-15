using System;
using System.Collections.Generic;

namespace SceneContent
{
    internal static class ComponentValidation
    {
        public static void Ensure(SceneDocument scene)
        {
            if (scene.Cameras == null || scene.Lights == null || scene.Cameras.Length > 1000 || scene.Lights.Length > 10000)
            {
                throw new ArgumentException("Invalid scene component count");
            }
            HashSet<int> cameraNodes = new HashSet<int>();
            foreach (SceneCamera camera in scene.Cameras)
            {
                if (camera == null || camera.Node < 0 || camera.Node >= scene.Nodes.Length || cameraNodes.Add(camera.Node) == false ||
                    (camera.Projection != "perspective" && camera.Projection != "orthographic") ||
                    Between(camera.FieldOfView, .01f, 179) == false || Between(camera.OrthographicSize, .00001f, 1000000) == false ||
                    Between(camera.Near, .00001f, 1000000) == false || Between(camera.Far, camera.Near + .00001f, 10000000) == false)
                {
                    throw new ArgumentException("Invalid camera binding or lens");
                }
                Color(camera.ClearColor, 4);
            }
            HashSet<int> lightNodes = new HashSet<int>();
            foreach (SceneLight light in scene.Lights)
            {
                if (light == null || light.Node < 0 || light.Node >= scene.Nodes.Length || lightNodes.Add(light.Node) == false ||
                    (light.Kind != "directional" && light.Kind != "point" && light.Kind != "spot") || light.IntensityUnit != "relative" ||
                    Between(light.Intensity, 0, 1000000) == false || Between(light.Range, .00001f, 1000000) == false ||
                    Between(light.SpotAngle, .01f, 179) == false)
                {
                    throw new ArgumentException("Invalid light binding or parameters");
                }
                Color(light.Color, 3);
            }
        }

        private static void Color(float[] values, int length)
        {
            if (values == null || values.Length != length)
            {
                throw new ArgumentException("Invalid color component count");
            }
            foreach (float value in values)
            {
                if (Between(value, 0, 1) == false)
                {
                    throw new ArgumentException("Color components must be finite and normalized");
                }
            }
        }

        private static bool Between(float value, float minimum, float maximum)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= minimum && value <= maximum;
        }
    }
}
