using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;
// `WitchMendokusai.NodeGraph` 가 namespace 와 class 동명 — ambiguity 회피 (TerrainGraph 와 같은 패턴).
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 영역 단위 erosion 노드. 입력 (worldX, worldZ, height) 받아 erosion 적용된 height 출력.
	///
	/// 노드 그래프는 *점 단위* (Pull) 모델이지만 erosion 은 *영역 단위* — 한 점 평가 위해 영역 전체 sim 필요.
	/// 해결: 노드 자체에 영역 캐시 (서브 결정 옵션 3) — `[NonSerialized]` Dictionary&lt;(regionX, regionZ), float[,]&gt; + lock.
	/// 첫 호출 시 영역 N×N input height sample → simulator 실행 → 캐시. 이후 같은 영역 점 호출은 lookup.
	///
	/// background thread 다발 호출 (chunk gen) 안전 — lock 으로 sim 1회만 실행.
	/// parameter 변경 시 hash 비교로 캐시 자동 invalidate.
	///
	/// region 좌표: worldX / regionSize 의 floor (음수 worldX 도 자연). 1 cell = 1 m 가정 (regionSize = N×N cells).
	/// </summary>
	[Serializable]
	public class HydraulicErosionNode : NodeBase
	{
		[Header("Region")]
		[SerializeField, Tooltip("영역 한 변 길이 (m). 1 m = 1 cell. 디폴트 256 — 강이 청크 경계 안 끊기는 적당 크기.")]
		[Range(64, 512)] private int regionSize = 256;

		[Header("Erosion Parameters")]
		[SerializeField] private HydraulicErosionParticleSimulator.Parameters parameters = HydraulicErosionParticleSimulator.Parameters.Default;

		private NodePort<float> inX;
		private NodePort<float> inZ;
		private NodePort<float> inHeight;
		private NodePort<float> outHeight;

		// region 캐시 — 노드 인스턴스 lifetime. 그래프 SO 로드 시 1번만 deserialize → 모든 점 호출 같은 인스턴스 공유.
		[NonSerialized] private readonly Dictionary<(int regionX, int regionZ), float[,]> regionCache = new();
		[NonSerialized] private readonly object cacheLock = new();
		[NonSerialized] private int lastParameterHash;
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
		/// (worldX, worldZ) 가 속한 영역의 eroded heightmap[regionSize, regionSize] 반환.
		/// 캐시 lookup → miss 시 lock → sample N×N input + simulator 실행 → 캐시 저장.
		/// parameter 변경 감지 (hash 비교) 시 전체 캐시 클리어.
		/// </summary>
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

			int currentHash = ComputeParameterHash();

			lock (cacheLock)
			{
				if (hasLastHash == false || lastParameterHash != currentHash)
				{
					regionCache.Clear();
					lastParameterHash = currentHash;
					hasLastHash = true;
				}

				if (regionCache.TryGetValue(regionKey, out float[,] cached))
					return cached;

				float[,] sampled = SampleSourceRegion(graph, sourceNode, regionX, regionZ);

				HydraulicErosionParticleSimulator.Parameters regionParams = parameters;
				// region 마다 다른 강 패턴 — seed 에 region 좌표 hash 섞음. 같은 region 같은 seed → deterministic.
				regionParams.seed = parameters.seed ^ (regionX * 73856093) ^ (regionZ * 19349663);
				HydraulicErosionParticleSimulator.Simulate(sampled, regionParams);

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
					// inHeight 의 source 노드를 직접 평가 → outputCache 에 결과 저장.
					subContext.Evaluate(sourceNode);
					// inHeight 가 source 노드의 output port 에 연결됨 — sub-context 에서 같은 connection 따라 GetInput 호출.
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

		private int ComputeParameterHash()
		{
			HashCode hash = new();
			hash.Add(regionSize);
			hash.Add(parameters.particleCount);
			hash.Add(parameters.maxParticleIterations);
			hash.Add(parameters.initialWater);
			hash.Add(parameters.initialVelocity);
			hash.Add(parameters.inertia);
			hash.Add(parameters.gravity);
			hash.Add(parameters.sedimentCapacityFactor);
			hash.Add(parameters.minSedimentCapacity);
			hash.Add(parameters.depositRate);
			hash.Add(parameters.erosionRate);
			hash.Add(parameters.evaporRate);
			hash.Add(parameters.seed);
			return hash.ToHashCode();
		}
	}
}
