using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	public partial class GroundGenerator
	{
		private const int MAX_DECORATION_BATCH_INSTANCES = 511;

		[Serializable]
		private sealed class DecorationSource
		{
			public GameObject Prefab;
			[Range(1, MAX_DECORATION_BATCH_INSTANCES)] public int CandidatesPerChunk = 100;
			public Vector2 ScaleRange = new(.7f, 1.4f);
		}

		private sealed class DecorationTemplate
		{
			public Mesh Mesh;
			public int Submesh;
			public Material Material;
			public Matrix4x4 Local;
			public ShadowCastingMode Shadows;
			public bool ReceiveShadows;
			public LightProbeUsage Probes;
			public int Layer;
			public DecorationSource Source;
			public int SourceSeed;
			public readonly Matrix4x4[] SubmissionMatrices = new Matrix4x4[MAX_DECORATION_BATCH_INSTANCES];
		}

		private sealed class DecorationBatch
		{
			public DecorationTemplate Template;
			public Matrix4x4[] Matrices;
			public int Count;
			public Bounds[] InstanceBounds;
			public Bounds Bounds;
		}

		private sealed class DecorationChunk
		{
			public Vector2Int Key;
			public bool Ready;
			public readonly List<DecorationBatch> Batches = new();
		}

		[Header("Procedural ground decoration")]
		[SerializeField] private DecorationSource[] decorationSources = Array.Empty<DecorationSource>();
		[SerializeField, Min(1f)] private float decorationChunkSize = 24f;
		[SerializeField, Range(1, 4)] private int decorationChunkRadius = 2;
		[SerializeField] private int decorationSeed = 4615;
		[SerializeField, Min(1f)] private float decorationClusterSize = 12f;
		[SerializeField, Range(0f, 1f)] private float decorationClusterThreshold = .43f;
		[SerializeField, Min(0f)] private float decorationSpawnClearRadius = 5f;
		[SerializeField, Min(0f)] private float decorationShadowMargin = 4f;
		[SerializeField] private float decorationHeight = .02f;

		private DecorationChunk[] decorationChunks = Array.Empty<DecorationChunk>();
		private bool[] decorationChunkVisible = Array.Empty<bool>();
		private readonly List<DecorationTemplate> decorationTemplates = new();
		private readonly Dictionary<Material, Material> decorationMaterials = new();
		private readonly Plane[] decorationPlanes = new Plane[6];
		private Vector2Int decorationCenter;
		private bool decorationCenterValid;

		public int DecorationVisibleChunks { get; private set; }
		public int DecorationDrawCalls { get; private set; }
		public int DecorationVisibleInstances { get; private set; }
		public int DecorationGeneratedChunks { get; private set; }

		private void OnEnable() => RenderPipelineManager.beginCameraRendering += RenderDecorations;

		private void OnDestroy()
		{
			foreach (Material material in decorationMaterials.Values)
				Destroy(material);
			decorationMaterials.Clear();
		}

		private void ReleaseDecorations()
		{
			RenderPipelineManager.beginCameraRendering -= RenderDecorations;
			decorationCenterValid = false;
			foreach (DecorationChunk chunk in decorationChunks)
				if (chunk != null)
					chunk.Ready = false;
		}

		private void LateUpdate()
		{
			if (decorationSources.Length == 0 || playerProvider.CurrentObject == null)
				return;
			if (decorationTemplates.Count == 0)
				BuildDecorationTemplates();
			int diameter = decorationChunkRadius * 2 + 1;
			if (decorationChunks.Length != diameter * diameter)
			{
				decorationChunks = new DecorationChunk[diameter * diameter];
				decorationChunkVisible = new bool[decorationChunks.Length];
				decorationCenterValid = false;
			}
			Vector3 position = playerProvider.CurrentObject.UnitMovement.Position;
			Vector2Int center = new(Mathf.FloorToInt(position.x / decorationChunkSize), Mathf.FloorToInt(position.z / decorationChunkSize));
			if (decorationCenterValid && center == decorationCenter)
				return;
			decorationCenter = center;
			decorationCenterValid = true;
			for (int x = -decorationChunkRadius; x <= decorationChunkRadius; x++)
				for (int z = -decorationChunkRadius; z <= decorationChunkRadius; z++)
				{
					Vector2Int key = center + new Vector2Int(x, z);
					int slot = ((key.x % diameter + diameter) % diameter) * diameter + (key.y % diameter + diameter) % diameter;
					DecorationChunk chunk = decorationChunks[slot] ??= new DecorationChunk();
					if (chunk.Ready == false || chunk.Key != key)
						BuildDecorationChunk(chunk, key);
				}
		}

		private void BuildDecorationTemplates()
		{
			int sourceIndex = 0;
			foreach (DecorationSource source in decorationSources)
			{
				foreach (MeshFilter filter in source.Prefab.GetComponentsInChildren<MeshFilter>())
				{
					MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
					Material[] materials = renderer.sharedMaterials;
					for (int index = 0; index < filter.sharedMesh.subMeshCount; index++)
					{
						Material original = materials[index];
						if (decorationMaterials.TryGetValue(original, out Material material) == false)
						{
							material = new Material(original) { name = original.name + " Decoration", enableInstancing = true };
							decorationMaterials.Add(original, material);
						}
						decorationTemplates.Add(new DecorationTemplate
						{
							Mesh = filter.sharedMesh, Submesh = index, Material = material,
							Local = Matrix4x4.TRS(Vector3.zero, source.Prefab.transform.localRotation, source.Prefab.transform.localScale) * source.Prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix,
							Shadows = renderer.shadowCastingMode, ReceiveShadows = renderer.receiveShadows, Probes = renderer.lightProbeUsage,
							Layer = renderer.gameObject.layer, Source = source, SourceSeed = sourceIndex * 83492791
						});
					}
				}
				sourceIndex++;
			}
		}

		private void BuildDecorationChunk(DecorationChunk chunk, Vector2Int key)
		{
			chunk.Key = key;
			chunk.Ready = true;
			DecorationGeneratedChunks++;
			if (chunk.Batches.Count == 0)
				foreach (DecorationTemplate template in decorationTemplates)
					chunk.Batches.Add(new DecorationBatch
					{
						Template = template, Matrices = new Matrix4x4[template.Source.CandidatesPerChunk],
						InstanceBounds = new Bounds[template.Source.CandidatesPerChunk]
					});
			foreach (DecorationBatch batch in chunk.Batches)
			{
				DecorationTemplate template = batch.Template;
				// 좌표 기반 난수. 청크 왕복과 생성 순서에 무관하고 전투 난수 상태 보존
				System.Random random = new(unchecked(decorationSeed ^ template.SourceSeed ^ key.x * 73856093 ^ key.y * 19349663));
				batch.Count = 0;
				Bounds bounds = new();
				for (int index = 0; index < template.Source.CandidatesPerChunk; index++)
				{
					Vector3 position = new((key.x + (float)random.NextDouble()) * decorationChunkSize, decorationHeight, (key.y + (float)random.NextDouble()) * decorationChunkSize);
					float rotation = (float)random.NextDouble() * 360f;
					float scale = Mathf.Lerp(template.Source.ScaleRange.x, template.Source.ScaleRange.y, (float)random.NextDouble());
					if (new Vector2(position.x, position.z).sqrMagnitude < decorationSpawnClearRadius * decorationSpawnClearRadius)
						continue;
					if (Mathf.PerlinNoise(position.x / decorationClusterSize, position.z / decorationClusterSize) < decorationClusterThreshold)
						continue;
					Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(0, rotation, 0), Vector3.one * scale) * template.Local;
					Bounds meshBounds = TransformDecorationBounds(template.Mesh.bounds, matrix);
					if (template.Shadows != ShadowCastingMode.Off)
						meshBounds.Expand(decorationShadowMargin * 2);
					if (batch.Count == 0) bounds = meshBounds;
					else bounds.Encapsulate(meshBounds);
					batch.InstanceBounds[batch.Count] = meshBounds;
					batch.Matrices[batch.Count++] = matrix;
				}
				batch.Bounds = bounds;
			}
		}

		private static Bounds TransformDecorationBounds(Bounds bounds, Matrix4x4 matrix)
		{
			Vector3 extents = bounds.extents;
			Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
			Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
			Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
			return new Bounds(matrix.MultiplyPoint3x4(bounds.center), new Vector3(
				Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
				Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
				Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)) * 2);
		}

		private void RenderDecorations(ScriptableRenderContext context, Camera camera)
		{
			if (camera.cameraType != CameraType.Game || camera.CompareTag("MainCamera") == false)
				return;
			DecorationVisibleChunks = 0;
			DecorationDrawCalls = 0;
			DecorationVisibleInstances = 0;
			Array.Clear(decorationChunkVisible, 0, decorationChunkVisible.Length);
			GeometryUtility.CalculateFrustumPlanes(camera, decorationPlanes);
			foreach (DecorationTemplate template in decorationTemplates)
			{
				int count = 0;
				Bounds submissionBounds = new();
				for (int chunkIndex = 0; chunkIndex < decorationChunks.Length; chunkIndex++)
				{
					DecorationChunk chunk = decorationChunks[chunkIndex];
					if (chunk == null || chunk.Ready == false) continue;
					foreach (DecorationBatch batch in chunk.Batches)
					{
						if (batch.Template != template || batch.Count == 0 || GeometryUtility.TestPlanesAABB(decorationPlanes, batch.Bounds) == false) continue;
						for (int index = 0; index < batch.Count; index++)
						{
							Bounds bounds = batch.InstanceBounds[index];
							if (GeometryUtility.TestPlanesAABB(decorationPlanes, bounds) == false) continue;
							decorationChunkVisible[chunkIndex] = true;
							if (count == 0) submissionBounds = bounds;
							else submissionBounds.Encapsulate(bounds);
							template.SubmissionMatrices[count++] = batch.Matrices[index];
							if (count == MAX_DECORATION_BATCH_INSTANCES)
							{
								SubmitDecorations(template, camera, count, submissionBounds);
								count = 0;
							}
						}
					}
				}
				if (count > 0) SubmitDecorations(template, camera, count, submissionBounds);
			}
			foreach (bool visible in decorationChunkVisible)
				if (visible) DecorationVisibleChunks++;
		}

		private void SubmitDecorations(DecorationTemplate template, Camera camera, int count, Bounds bounds)
		{
			RenderParams parameters = new(template.Material)
			{
				camera = camera, worldBounds = bounds, layer = template.Layer,
				shadowCastingMode = template.Shadows, receiveShadows = template.ReceiveShadows,
				lightProbeUsage = template.Probes
			};
			Graphics.RenderMeshInstanced(parameters, template.Mesh, template.Submesh, template.SubmissionMatrices, count);
			DecorationDrawCalls++;
			DecorationVisibleInstances += count;
		}
	}
}
