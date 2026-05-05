using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Voronoi cellular noise — 셀 격자 + 셀별 임의 점 → 가까운 점까지 거리. 매끈한 Perlin 과 다른 *돌 같은* 다각형 패턴.
	/// 입력 (worldX, worldZ) + 필드 (cellSize / amplitude / seed). 출력 height.
	///
	/// 공식: 셀 격자 (cellSize 단위), 각 셀 안 hash 기반 결정적 random 점, 인근 9 셀 중 가장 가까운 점까지 distance.
	/// 0 (셀 중심) ~ ~1 (셀 경계) 범위 정규화 → ±amplitude.
	/// </summary>
	[Serializable]
	public class VoronoiNode : NodeBase
	{
		[SerializeField] private float cellSize = 32f;
		[SerializeField] private float amplitude = 32f;
		[SerializeField] private int seed = 0;

		private NodePort<float> inX;
		private NodePort<float> inZ;
		private NodePort<float> outHeight;

		public float CellSize { get => cellSize; set => cellSize = Mathf.Max(0.1f, value); }
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

			int cellX = Mathf.FloorToInt(x / cellSize);
			int cellZ = Mathf.FloorToInt(z / cellSize);

			float minDistSqr = float.MaxValue;
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					int nx = cellX + dx;
					int nz = cellZ + dz;
					float pointWX = (nx + Hash01(nx, nz, seed)) * cellSize;
					float pointWZ = (nz + Hash01(nx, nz, seed + 1000)) * cellSize;
					float ddx = x - pointWX;
					float ddz = z - pointWZ;
					float distSqr = ddx * ddx + ddz * ddz;
					if (distSqr < minDistSqr)
						minDistSqr = distSqr;
				}
			}

			// 셀 경계 거리 ≈ cellSize/2 ~ cellSize. 정규화 → 0~1, 평균 ~0.5.
			float dist = Mathf.Sqrt(minDistSqr);
			float normalized = Mathf.Clamp01(dist / cellSize);
			context.SetOutput(outHeight, (normalized * 2f - 1f) * amplitude);
		}

		/// <summary>(int, int, seed) → 0~1 결정적 hash. PCG 식 단순 mix.</summary>
		private static float Hash01(int x, int z, int seed)
		{
			uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(seed * 83492791);
			h = (h ^ (h >> 13)) * 0x5BD1E995U;
			h = h ^ (h >> 15);
			return (h & 0x00FFFFFFU) / (float)0x01000000U;
		}
	}
}
