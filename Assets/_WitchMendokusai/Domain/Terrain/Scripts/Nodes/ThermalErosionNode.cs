using System;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Thermal erosion 노드 (angle of repose). 산봉우리 둥글게 깎임 + 절벽 안정화.
	/// Hydraulic 의 강·계곡과 다른 erosion 종류 — 같은 그래프에서 체이닝 가능 (Perlin → Hydraulic → Thermal → Output).
	///
	/// 영역 캐시 / lock / sub-context 인프라는 <see cref="RegionGridNodeBase"/> 베이스 책임.
	/// 본 sub class 는 알고리즘 위임 (`ThermalErosionGridSimulator`) + parameter hash 만 정의.
	/// G3 (2026-05-06) 신규.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class ThermalErosionNode : RegionGridNodeBase
	{
		[Header("Thermal Parameters")]
		[SerializeField] private ThermalErosionGridSimulator.Parameters parameters = ThermalErosionGridSimulator.Parameters.Default;

		protected override void Simulate(float[,] heightmap, int regionX, int regionZ)
		{
			// thermal 은 deterministic — region 좌표 hash 영향 X (random spawn 없음).
			ThermalErosionGridSimulator.Simulate(heightmap, parameters);
		}

		protected override int ComputeAlgorithmHash()
		{
			HashCode hash = new();
			hash.Add(parameters.iterations);
			hash.Add(parameters.talusAngle);
			hash.Add(parameters.strength);
			return hash.ToHashCode();
		}
	}
}
