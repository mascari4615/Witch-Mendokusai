using System;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 입자 기반 hydraulic erosion 노드. 강·V자 골짜기·토사 둑 패턴.
	///
	/// 영역 캐시 / lock / sub-context 인프라는 <see cref="RegionGridNodeBase"/> 베이스 책임.
	/// 본 sub class 는 알고리즘 위임 (`HydraulicErosionParticleSimulator`) + parameter hash 만 정의.
	/// G3 (2026-05-06) refactor — 코드 230줄 → 50줄.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class HydraulicErosionNode : RegionGridNodeBase
	{
		[Header("Hydraulic Parameters")]
		[SerializeField] private HydraulicErosionParticleSimulator.Parameters parameters = HydraulicErosionParticleSimulator.Parameters.Default;

		protected override void Simulate(float[,] heightmap, int regionX, int regionZ)
		{
			HydraulicErosionParticleSimulator.Parameters regionParams = parameters;
			// region 마다 다른 강 패턴 — seed 에 region 좌표 hash 섞음.
			regionParams.seed = parameters.seed ^ (regionX * 73856093) ^ (regionZ * 19349663);
			HydraulicErosionParticleSimulator.Simulate(heightmap, regionParams);
		}

		protected override int ComputeAlgorithmHash()
		{
			// 결정적 해시 (디스크 영속 키 — System.HashCode 금지, TASK-WM-119).
			return StableCombine(
				parameters.particleCount,
				parameters.maxParticleIterations,
				FloatBits(parameters.initialWater),
				FloatBits(parameters.initialVelocity),
				FloatBits(parameters.inertia),
				FloatBits(parameters.gravity),
				FloatBits(parameters.sedimentCapacityFactor),
				FloatBits(parameters.minSedimentCapacity),
				FloatBits(parameters.depositRate),
				FloatBits(parameters.erosionRate),
				FloatBits(parameters.evaporRate),
				parameters.seed);
		}
	}
}
