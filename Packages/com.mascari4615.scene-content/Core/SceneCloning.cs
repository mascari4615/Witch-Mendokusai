namespace SceneContent
{
    public static class SceneCloning
    {
        public static SceneDocument Copy(SceneDocument source)
        {
            SceneValidation.Ensure(source);
            SceneDocument result = new SceneDocument
            {
                Id = source.Id, Version = source.Version, Nodes = new SceneNode[source.Nodes.Length], Meshes = new MeshInstance[source.Meshes.Length],
                Materials = new SurfaceMaterial[source.Materials.Length], Resources = new ContentResource[source.Resources.Length],
                Cameras = new SceneCamera[source.Cameras.Length], Lights = new SceneLight[source.Lights.Length], Parameters = SceneParameters.Copy(source.Parameters),
                LimbConstraints = SceneConstraints.Copy(source.LimbConstraints),
                Springs = SceneDynamics.Copy(source.Springs), Colliders = SceneDynamics.Copy(source.Colliders)
            };
            for (int index = 0; index < source.Nodes.Length; index++)
            {
                SceneNode value = source.Nodes[index];
                result.Nodes[index] = new SceneNode
                {
                    Id = value.Id, Label = value.Label, Parent = value.Parent,
                    Position = (float[])value.Position.Clone(), Rotation = (float[])value.Rotation.Clone(), Scale = (float[])value.Scale.Clone()
                };
            }
            for (int index = 0; index < source.Meshes.Length; index++)
            {
                MeshInstance value = source.Meshes[index];
                result.Meshes[index] = new MeshInstance
                {
                    Node = value.Node, RootBone = value.RootBone, Geometry = value.Geometry, ShadowOnly = value.ShadowOnly, Hidden = value.Hidden,
                    Bones = (int[])value.Bones.Clone(), Materials = (int[])value.Materials.Clone(), MorphWeights = (float[])value.MorphWeights.Clone()
                };
            }
            for (int index = 0; index < source.Materials.Length; index++)
            {
                result.Materials[index] = CopySurface(source.Materials[index]);
            }
            for (int index = 0; index < source.Resources.Length; index++)
            {
                ContentResource value = source.Resources[index];
                result.Resources[index] = new ContentResource { Id = value.Id, Path = value.Path, MediaType = value.MediaType, Sha256 = value.Sha256 };
            }
            for (int index = 0; index < source.Cameras.Length; index++)
            {
                SceneCamera value = source.Cameras[index];
                result.Cameras[index] = new SceneCamera
                {
                    Node = value.Node, Projection = value.Projection, FieldOfView = value.FieldOfView, OrthographicSize = value.OrthographicSize,
                    Near = value.Near, Far = value.Far, ClearColor = (float[])value.ClearColor.Clone(), Enabled = value.Enabled
                };
            }
            for (int index = 0; index < source.Lights.Length; index++)
            {
                SceneLight value = source.Lights[index];
                result.Lights[index] = new SceneLight
                {
                    Node = value.Node, Kind = value.Kind, Color = (float[])value.Color.Clone(), Intensity = value.Intensity, IntensityUnit = value.IntensityUnit,
                    Range = value.Range, SpotAngle = value.SpotAngle, Shadows = value.Shadows, Enabled = value.Enabled
                };
            }
            return result;
        }

        public static SurfaceMaterial CopySurface(SurfaceMaterial value)
        {
            return new SurfaceMaterial
            {
                BaseColor = (float[])value.BaseColor.Clone(), BaseColorTexture = value.BaseColorTexture,
                AlphaMode = value.AlphaMode, DoubleSided = value.DoubleSided,
                TextureScale = (float[])value.TextureScale.Clone(), TextureOffset = (float[])value.TextureOffset.Clone(), TextureWrap = value.TextureWrap,
                LightingModel = value.LightingModel, Roughness = value.Roughness, Metallic = value.Metallic, Emission = (float[])value.Emission.Clone(),
                MaterialClass = value.MaterialClass, ToonSurface = value.ToonSurface == null ? null : value.ToonSurface.Copy()
            };
        }
    }
}
