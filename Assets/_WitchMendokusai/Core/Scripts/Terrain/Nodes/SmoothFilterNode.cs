using System;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Smooth filter 노드 (Box N-pass blur). Erosion 후 noise 정리 + 강·골짜기 부드러운 윤곽.
	///
	/// 영역 캐시 / lock / sub-context 인프라는 <see cref="RegionGridNodeBase"/> 베이스 책임.
	/// 본 sub class 는 알고리즘 위임 (`SmoothGridSimulator`) + parameter hash 만 정의.
	/// H1 (2026-05-06) 신규.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class SmoothFilterNode : RegionGridNodeBase
	{
		[Header("Smooth Parameters")]
		[SerializeField] private SmoothGridSimulator.Parameters parameters = SmoothGridSimulator.Parameters.Default;

		protected override void Simulate(float[,] heightmap, int regionX, int regionZ)
		{
			// smooth 는 deterministic — region 좌표 hash 영향 X (random spawn 없음).
			SmoothGridSimulator.Simulate(heightmap, parameters);
		}

		protected override int ComputeAlgorithmHash()
		{
			return parameters.iterations.GetHashCode();
		}
	}
}
