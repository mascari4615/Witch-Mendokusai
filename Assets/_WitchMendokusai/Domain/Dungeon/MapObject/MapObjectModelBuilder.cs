using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// MapObjectModel 부품을 자식 메시로 지음. 같은 모양과 크기의 메시는 한 번만 만들어 같이 씀
	public static class MapObjectModelBuilder
	{
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly Dictionary<string, Mesh> meshes = new();
		private static MaterialPropertyBlock propertyBlock;

		public static void Build(MapObjectModel model, Transform parent, MapObjectSettings settings)
		{
			GameObject root = new("Model");
			root.transform.SetParent(parent, false);
			MapObjectModelMotion motion = null;
			foreach (MapObjectModelPart part in model.Parts)
			{
				Mesh mesh = GetMesh(part);
				for (int i = 0; i < part.RadialCount; i++)
				{
					GameObject piece = new(part.Shape.ToString());
					piece.transform.SetParent(root.transform, false);
					float yaw = part.RadialStartAngle + 360f * i / part.RadialCount;
					Quaternion around = Quaternion.Euler(0f, yaw, 0f);
					Vector3 radial = part.RadialCount > 1 ? around * (Vector3.forward * part.RadialRadius) : Vector3.zero;
					piece.transform.localPosition = radial + part.Position;
					piece.transform.localRotation = (part.RadialCount > 1 ? around : Quaternion.identity) * Quaternion.Euler(part.Rotation);
					piece.AddComponent<MeshFilter>().sharedMesh = mesh;
					MeshRenderer meshRenderer = piece.AddComponent<MeshRenderer>();
					meshRenderer.shadowCastingMode = part.Surface == MapObjectPartSurface.Glow ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
					if (part.Surface == MapObjectPartSurface.Glow)
						meshRenderer.sharedMaterial = settings.MarkerMaterial;
					else
					{
						meshRenderer.sharedMaterial = settings.StoneMaterial;
						propertyBlock ??= new MaterialPropertyBlock();
						propertyBlock.SetColor(BaseColorId, part.Color);
						meshRenderer.SetPropertyBlock(propertyBlock);
					}
					if (part.Solid)
					{
						BoxCollider box = piece.AddComponent<BoxCollider>();
						box.center = mesh.bounds.center;
						box.size = mesh.bounds.size;
					}
					if (part.SpinSpeed != 0f || part.BobHeight > 0f)
					{
						if (motion == null)
							motion = root.AddComponent<MapObjectModelMotion>();
						motion.Add(piece.transform, part.SpinSpeed, part.BobHeight, part.BobPeriod, (float)i / part.RadialCount);
					}
				}
			}
		}

		private static Mesh GetMesh(MapObjectModelPart part)
		{
			string key = $"{part.Shape}|{part.Sides}|{part.TopScale}|{part.Size}|{part.Color}";
			if (meshes.TryGetValue(key, out Mesh cached) && cached != null)
				return cached;
			Mesh mesh = MapObjectModelMesh.Build(part.Shape, part.Size, part.Sides, part.TopScale, part.Color);
			mesh.name = "MapObjectPart " + key;
			mesh.hideFlags = HideFlags.DontSave;
			meshes[key] = mesh;
			return mesh;
		}
	}
}
