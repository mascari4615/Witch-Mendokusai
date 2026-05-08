using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 그리드 기반 thermal erosion (angle of repose). 각 cell 의 인접 8 cell 과 height 차이가 talus (자연 안식각) 보다
	/// 크면 → 그 neighbor 로 고도 redistribute. N iteration 부드럽게 수렴.
	///
	/// 시각: 산봉우리 둥글게 깎임 + 절벽 경사 안정화. Hydraulic 의 강·계곡과 다른 erosion 종류 — 체이닝 자연.
	///
	/// 알고리즘 (Olsen 1995 식, 단순 변형):
	/// 1. 각 cell 의 8 neighbor 중 max diff 찾기
	/// 2. max diff > talus 면, talus 초과 neighbor 들로 (max diff - talus) * strength * 0.5 redistribute
	/// 3. 분배 비율 = neighbor 의 (diff - talus) / 전체 talus 초과 합 (가파른 neighbor 일수록 더 많이)
	/// 4. delta 별도 배열 누적 후 한 번에 적용 — iteration 안 race 회피
	///
	/// NodeGraph / Unity 의존 X (Mathf 만) — 단위 테스트 가능.
	/// </summary>
	public static class ThermalErosionGridSimulator
	{
		[Serializable]
		public struct Parameters
		{
			[Tooltip("iteration 수. 클수록 더 부드럽게 수렴 / 느림. 디폴트 8.")]
			public int iterations;

			[Tooltip("자연 안식각 (도). 이 각도 보다 가파른 경사는 무너짐. 디폴트 35° (자연 흙·자갈 평균).")]
			[Range(15f, 75f)] public float talusAngle;

			[Tooltip("재분배 강도 (0=무 변동, 1=즉시 평탄). 디폴트 0.5.")]
			[Range(0f, 1f)] public float strength;

			public static Parameters Default => new()
			{
				iterations = 8,
				talusAngle = 35f,
				strength = 0.5f,
			};
		}

		public static void Simulate(float[,] heightmap, Parameters parameters)
		{
			if (heightmap == null)
				return;

			int width = heightmap.GetLength(0);
			int height = heightmap.GetLength(1);
			if (width < 2 || height < 2)
				return;

			int iterations = Mathf.Max(1, parameters.iterations);
			float talusThreshold = Mathf.Tan(parameters.talusAngle * Mathf.Deg2Rad);
			float strength = Mathf.Clamp01(parameters.strength);

			float[,] delta = new float[width, height];

			for (int iter = 0; iter < iterations; iter++)
			{
				// delta 0 으로 초기화
				for (int x = 0; x < width; x++)
				{
					for (int z = 0; z < height; z++)
					{
						delta[x, z] = 0f;
					}
				}

				for (int x = 0; x < width; x++)
				{
					for (int z = 0; z < height; z++)
					{
						AccumulateDeltaForCell(heightmap, delta, width, height, x, z, talusThreshold, strength);
					}
				}

				// delta 적용
				for (int x = 0; x < width; x++)
				{
					for (int z = 0; z < height; z++)
					{
						heightmap[x, z] += delta[x, z];
					}
				}
			}
		}

		private static void AccumulateDeltaForCell(
			float[,] heightmap, float[,] delta,
			int width, int height,
			int x, int z, float talusThreshold, float strength)
		{
			float currentHeight = heightmap[x, z];

			// 1 패스: 8 neighbor 중 가장 큰 diff
			float maxDiff = 0f;
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dz == 0)
						continue;
					int nx = x + dx;
					int nz = z + dz;
					if (nx < 0 || nx >= width || nz < 0 || nz >= height)
						continue;

					float diff = currentHeight - heightmap[nx, nz];
					if (diff > maxDiff)
						maxDiff = diff;
				}
			}

			if (maxDiff <= talusThreshold)
				return;

			// 2 패스: talus 초과 neighbor 들 (diff - talus) 합산
			float totalExcess = 0f;
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dz == 0)
						continue;
					int nx = x + dx;
					int nz = z + dz;
					if (nx < 0 || nx >= width || nz < 0 || nz >= height)
						continue;

					float diff = currentHeight - heightmap[nx, nz];
					if (diff > talusThreshold)
						totalExcess += diff - talusThreshold;
				}
			}

			if (totalExcess <= 0f)
				return;

			float amountToMove = (maxDiff - talusThreshold) * strength * 0.5f;

			// 3 패스: 각 neighbor 비율로 분배 (delta 누적)
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					if (dx == 0 && dz == 0)
						continue;
					int nx = x + dx;
					int nz = z + dz;
					if (nx < 0 || nx >= width || nz < 0 || nz >= height)
						continue;

					float diff = currentHeight - heightmap[nx, nz];
					if (diff > talusThreshold)
					{
						float share = (diff - talusThreshold) / totalExcess;
						float moved = amountToMove * share;
						delta[nx, nz] += moved;
						delta[x, z] -= moved;
					}
				}
			}
		}
	}
}
