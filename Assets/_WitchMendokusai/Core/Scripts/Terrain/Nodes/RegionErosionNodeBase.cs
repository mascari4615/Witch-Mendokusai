using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;
// `WitchMendokusai.NodeGraph` 가 namespace 와 class 동명 — ambiguity 회피.
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 영역 단위 erosion 노드 베이스. Hydraulic / Thermal / (후속 Compute) 등 모든 erosion 노드 공통 인프라.
	///
	/// 노드 그래프는 *점 단위* (Pull) 모델, erosion 은 *영역 단위* — 한 점 평가 위해 영역 전체 sim 필요.
	/// 해결: 노드 자체에 영역 캐시 (서브 결정 옵션 3) — `[NonSerialized]` Dictionary + lock.
	/// 첫 호출 시 영역 N×N input height sample → sub class `Simulate` → 캐시. 이후 같은 영역 점 호출은 lookup.
	///
	/// background thread 다발 호출 (chunk gen) 안전. parameter 변경 시 hash 비교로 캐시 자동 invalidate.
	///
	/// region 좌표: worldX / regionSize 의 floor (음수 worldX 도 자연). 1 cell = 1 m 가정.
	/// region 마다 다른 결과 — sub class hash 가 region 좌표 안 섞도록 책임 (또는 베이스가 처리).
	/// </summary>
	[Serializable]
	public abstract class RegionErosionNodeBase : NodeBase
	{
		[Header("Region")]
		[SerializeField, Tooltip("영역 한 변 길이 (m). 1 m = 1 cell. 디폴트 256.")]
		[Range(64, 512)] protected int regionSize = 256;

		private NodePort<float> inX;
		private NodePort<float> inZ;
		private NodePort<float> inHeight;
		private NodePort<float> outHeight;

		// region 캐시 — 노드 인스턴스 lifetime. 그래프 SO 로드 시 1번만 deserialize → 모든 점 호출 같은 인스턴스 공유.
		[NonSerialized] private readonly Dictionary<(int regionX, int regionZ), float[,]> regionCache = new();
		[NonSerialized] private readonly object cacheLock = new();
		[NonSerialized] private int lastAlgorithmHash;
		[NonSerialized] private bool hasLastHash;

		public NodePort<float> HeightInput
		{
			get
			{
				_ = Ports;
				return inHeight;
			}
		}

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inX = new NodePort<float>(this, "x", PortDirection.Input);
			inZ = new NodePort<float>(this, "z", PortDirection.Input);
			inHeight = new NodePort<float>(this, "height", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inX;
			yield return inZ;
			yield return inHeight;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float worldX = context.GetInput(inX);
			float worldZ = context.GetInput(inZ);

			float[,] cachedRegion = GetOrComputeRegion(context, worldX, worldZ);
			if (cachedRegion == null)
			{
				// fallback — connection 없거나 graph null. input height 그대로.
				context.SetOutput(outHeight, context.GetInput(inHeight));
				return;
			}

			int regionX = Mathf.FloorToInt(worldX / regionSize);
			int regionZ = Mathf.FloorToInt(worldZ / regionSize);
			float regionWorldX = regionX * regionSize;
			float regionWorldZ = regionZ * regionSize;

			float localX = worldX - regionWorldX;
			float localZ = worldZ - regionWorldZ;

			float erodedHeight = SampleBilinear(cachedRegion, localX, localZ);
			context.SetOutput(outHeight, erodedHeight);
		}

		/// <summary>
		/// sub class 가 알고리즘 본체 구현 — heightmap[regionSize, regionSize] in-place erosion.
		/// regionX/regionZ 는 region 격자 좌표 (deterministic seed 변형 등에 사용).
		/// </summary>
		protected abstract void Simulate(float[,] heightmap, int regionX, int regionZ);

		/// <summary>
		/// sub class 의 parameter hash — 베이스 regionSize 와 함께 캐시 invalidation 키.
		/// parameter 변경 시 hash 다르면 캐시 전체 클리어.
		/// </summary>
		protected abstract int ComputeAlgorithmHash();

		private float[,] GetOrComputeRegion(NodeExecutionContext context, float worldX, float worldZ)
		{
			NodeGraphAsset graph = context.Graph;
			if (graph == null)
				return null;

			NodeConnection heightConn = graph.FindConnectionToInput(inHeight);
			if (heightConn == null)
				return null;

			NodeBase sourceNode = graph.FindNode(heightConn.SourceNodeId);
			if (sourceNode == null)
				return null;

			int regionX = Mathf.FloorToInt(worldX / regionSize);
			int regionZ = Mathf.FloorToInt(worldZ / regionSize);
			(int, int) regionKey = (regionX, regionZ);

			int currentHash = HashCode.Combine(regionSize, ComputeAlgorithmHash());

			lock (cacheLock)
			{
				if (hasLastHash == false || lastAlgorithmHash != currentHash)
				{
					regionCache.Clear();
					lastAlgorithmHash = currentHash;
					hasLastHash = true;
				}

				if (regionCache.TryGetValue(regionKey, out float[,] cached))
					return cached;

				float[,] sampled = SampleSourceRegion(graph, sourceNode, regionX, regionZ);
				Simulate(sampled, regionX, regionZ);

				regionCache[regionKey] = sampled;
				return sampled;
			}
		}

		/// <summary>
		/// (regionX, regionZ) 영역의 N×N input height 를 sub-context 로 sample.
		/// outer context 의 evaluated set 때문에 source 노드 재평가 안 됨 → 매 cell 마다 새 sub-context.
		/// </summary>
		private float[,] SampleSourceRegion(NodeGraphAsset graph, NodeBase sourceNode, int regionX, int regionZ)
		{
			float[,] heights = new float[regionSize, regionSize];
			float regionWorldX = regionX * regionSize;
			float regionWorldZ = regionZ * regionSize;

			for (int cellX = 0; cellX < regionSize; cellX++)
			{
				for (int cellZ = 0; cellZ < regionSize; cellZ++)
				{
					float sampleWorldX = regionWorldX + cellX;
					float sampleWorldZ = regionWorldZ + cellZ;

					NodeExecutionContext subContext = new(graph);
					subContext.SetGlobalInput(WorldPositionInputNode.KEY_WORLD_X, sampleWorldX);
					subContext.SetGlobalInput(WorldPositionInputNode.KEY_WORLD_Z, sampleWorldZ);
					subContext.Evaluate(sourceNode);
					heights[cellX, cellZ] = subContext.GetInput(inHeight);
				}
			}

			return heights;
		}

		private static float SampleBilinear(float[,] map, float x, float z)
		{
			int width = map.GetLength(0);
			int height = map.GetLength(1);
			int x0 = Mathf.Clamp((int)x, 0, width - 1);
			int z0 = Mathf.Clamp((int)z, 0, height - 1);
			int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
			int z1 = Mathf.Clamp(z0 + 1, 0, height - 1);
			float fracX = Mathf.Clamp01(x - x0);
			float fracZ = Mathf.Clamp01(z - z0);

			float h00 = map[x0, z0];
			float h10 = map[x1, z0];
			float h01 = map[x0, z1];
			float h11 = map[x1, z1];
			return h00 * (1f - fracX) * (1f - fracZ) + h10 * fracX * (1f - fracZ) + h01 * (1f - fracX) * fracZ + h11 * fracX * fracZ;
		}
	}
}
