using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Voronoi (cellular noise) generator. 셀 격자 + hash 기반 결정적 random 점 + 인근 9셀 최단거리 -> ±amplitude.
	/// 결과: 다각형 고원/분지 지형. FractalPerlinNode 와 교체해 사용.
	/// </summary>
	[Serializable]
	public class VoronoiNode : NodeBase
	{
		[SerializeField] private float cellSize = 64f;
		[SerializeField] private float amplitude = 32f;
		[SerializeField] private int seed = 0;

		private const float COORD_OFFSET = 100000f;

		private NodePort<float> inX;
		private NodePort<float> inZ;
		private NodePort<float> outHeight;

		public float CellSize { get => cellSize; set => cellSize = Mathf.Max(1f, value); }
		public float Amplitude { get => amplitude; set => amplitude = value; }
		public int Seed { get => seed; set => seed = value; }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inX = new NodePort<float>(this, "x", PortDirection.Input);
			inZ = new NodePort<float>(this, "z", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inX;
			yield return inZ;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float x = context.GetInput(inX);
			float z = context.GetInput(inZ);

			float px = (x + COORD_OFFSET) / cellSize;
			float pz = (z + COORD_OFFSET) / cellSize;

			int cellX = Mathf.FloorToInt(px);
			int cellZ = Mathf.FloorToInt(pz);

			float minDistSq = float.MaxValue;

			for (int dz = -1; dz <= 1; dz++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int nx = cellX + dx;
					int nz = cellZ + dz;

					float rx = HashToFloat(nx, nz, seed, 0);
					float rz = HashToFloat(nx, nz, seed, 1);

					float pointX = nx + rx;
					float pointZ = nz + rz;

					float distX = px - pointX;
					float distZ = pz - pointZ;
					float distSq = distX * distX + distZ * distZ;

					if (distSq < minDistSq)
						minDistSq = distSq;
				}
			}

			// 최대 거리 = 셀 내 코너 대각선 절반 ~= sqrt(2)/2. [0,1] 정규화 후 +-amplitude 매핑.
			float normalized = Mathf.Clamp01(Mathf.Sqrt(minDistSq) / 0.7072f);
			context.SetOutput(outHeight, (normalized * 2f - 1f) * amplitude);
		}

		private static float HashToFloat(int cellX, int cellZ, int seed, int channel)
		{
			int hash = cellX * 1619 + cellZ * 31337 + seed * 6971 + channel * 1013;
			hash = hash ^ (hash >> 13);
			hash = hash * (hash * hash * 15731 + 789221) + 1376312589;
			return (hash & 0x7fffffff) / (float)0x7fffffff;
		}
	}
}