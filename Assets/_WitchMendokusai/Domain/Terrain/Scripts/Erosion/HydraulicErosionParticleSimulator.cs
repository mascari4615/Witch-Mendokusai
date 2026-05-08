using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 입자 기반 hydraulic erosion 알고리즘 (Sebastian Lague 식). heightmap[N×N] 입력 → 같은 크기 eroded heightmap 출력.
	///
	/// 입자 N개를 영역 random 위치 spawn → 경사 따라 흐름·깎임·퇴적·증발 simulate. 시드 동일 → 동일 결과 (결정성 보장).
	/// NodeGraph / Unity 의존 X (Mathf 만) — 단위 테스트 가능.
	///
	/// 알고리즘 단계 (per particle, per iteration):
	/// 1. 현재 위치의 height + gradient 를 bilinear sample
	/// 2. velocity = velocity * (1 - inertia) + (-gradient * gravity)  — gradient 가 입자를 내리막으로 끌어당김
	/// 3. position += velocity 정규화 — 1 셀씩 step
	/// 4. new height sample → deltaHeight 계산
	/// 5. sedimentCapacity = max(-deltaHeight * speed * water * sedimentCapacityFactor, minCapacity)
	/// 6. sediment > capacity 또는 오르막 (deltaHeight > 0) → *deposit* (heightmap 늘림, sediment 줄임)
	///    아니면 → *erode* (heightmap 깎음, sediment 늘림). 단 erode 는 -deltaHeight 한계 (홀 방지)
	/// 7. water *= (1 - evaporRate). 영역 밖 또는 water 0 → particle 종료
	/// </summary>
	public static class HydraulicErosionParticleSimulator
	{
		/// <summary>
		/// 알고리즘 파라미터 묶음. 노드 / 호출자가 SerializeField 로 노출 → 사용자가 슬라이더 조정.
		/// 디폴트 톤 = 중간 (Mojang 거친 ↔ Stardew 부드러운 사이).
		/// </summary>
		[Serializable]
		public struct Parameters
		{
			[Tooltip("발사할 입자 수. 클수록 깎임 더 풍부 / 느림. 디폴트 50000.")]
			public int particleCount;

			[Tooltip("입자당 최대 step 수. 도달 못 한 입자 cutoff. 디폴트 30.")]
			public int maxParticleIterations;

			[Tooltip("입자 시작 water (퇴적 capacity 곱셈에 영향). 디폴트 1.")]
			public float initialWater;

			[Tooltip("입자 시작 velocity. 디폴트 1.")]
			public float initialVelocity;

			[Tooltip("관성 (이전 velocity 유지 비율). 0=중력만, 1=관성만. 디폴트 0.05 — 거의 중력 따름.")]
			[Range(0f, 1f)] public float inertia;

			[Tooltip("중력 가속. velocity 에 -gradient * gravity 더함. 디폴트 4.")]
			public float gravity;

			[Tooltip("sediment capacity 곱셈 — 클수록 입자가 더 많이 운반. 디폴트 4.")]
			public float sedimentCapacityFactor;

			[Tooltip("최소 sediment capacity (평지에서도 약간 운반 가능). 디폴트 0.01.")]
			public float minSedimentCapacity;

			[Tooltip("over-capacity 시 deposit 비율 (0=즉시 다 쌓음, 1=천천히). 디폴트 0.3.")]
			[Range(0f, 1f)] public float depositRate;

			[Tooltip("under-capacity 시 erode 비율. 디폴트 0.3.")]
			[Range(0f, 1f)] public float erosionRate;

			[Tooltip("물 증발 비율 (per iteration). 클수록 입자 빨리 사라짐. 디폴트 0.01.")]
			[Range(0f, 0.5f)] public float evaporRate;

			[Tooltip("결정성 시드. 같은 시드 + 같은 영역 → 같은 결과.")]
			public int seed;

			public static Parameters Default => new()
			{
				// G1 — visible 임팩트 디폴트 톤. 사용자가 슬라이더로 (a) 미니멀 ↔ (c) 거친 자유 전환.
				particleCount = 100000,
				maxParticleIterations = 50,
				initialWater = 1f,
				initialVelocity = 1f,
				inertia = 0.05f,
				gravity = 4f,
				sedimentCapacityFactor = 4f,
				minSedimentCapacity = 0.01f,
				depositRate = 0.3f,
				erosionRate = 0.5f,
				evaporRate = 0.01f,
				seed = 0,
			};
		}

		/// <summary>
		/// heightmap[width, height] 을 in-place 또는 별도 출력으로 erosion 적용.
		/// 호출자가 input 복사본을 넘기면 결과는 그 배열 — 안전.
		/// width/height 는 heightmap 의 GetLength(0)/GetLength(1). 정사각 가정 권장 (정사각 아니어도 동작).
		/// </summary>
		public static void Simulate(float[,] heightmap, Parameters parameters)
		{
			if (heightmap == null)
				return;

			int width = heightmap.GetLength(0);
			int height = heightmap.GetLength(1);
			if (width < 2 || height < 2)
				return;

			int particleCount = Mathf.Max(0, parameters.particleCount);
			int maxIter = Mathf.Max(1, parameters.maxParticleIterations);

			// 결정적 random — System.Random 시드 기반.
			System.Random random = new(parameters.seed);

			for (int p = 0; p < particleCount; p++)
			{
				SimulateOneParticle(heightmap, width, height, parameters, maxIter, random);
			}
		}

		private static void SimulateOneParticle(
			float[,] heightmap,
			int width,
			int height,
			Parameters parameters,
			int maxIter,
			System.Random random)
		{
			// Spawn — 영역 안 random 위치 (cell 좌표 [0, width-1) x [0, height-1)).
			float positionX = (float)(random.NextDouble() * (width - 1));
			float positionZ = (float)(random.NextDouble() * (height - 1));
			float velocityX = 0f;
			float velocityZ = 0f;
			float water = parameters.initialWater;
			float sediment = 0f;

			for (int iter = 0; iter < maxIter; iter++)
			{
				int cellX = (int)positionX;
				int cellZ = (int)positionZ;
				float fracX = positionX - cellX;
				float fracZ = positionZ - cellZ;

				// 현재 위치 height + gradient — bilinear interpolation 으로 부드럽게.
				HeightAndGradient hag = SampleHeightAndGradient(heightmap, width, height, cellX, cellZ, fracX, fracZ);

				// velocity update — 관성 유지 + 중력 (gradient 의 반대 방향이 내리막).
				velocityX = velocityX * (1f - parameters.inertia) + (-hag.gradientX * parameters.gravity);
				velocityZ = velocityZ * (1f - parameters.inertia) + (-hag.gradientZ * parameters.gravity);

				// 정규화 — 입자를 한 cell 정도씩 step (속도 무관 stable).
				float speed = Mathf.Sqrt(velocityX * velocityX + velocityZ * velocityZ);
				if (speed < 0.0001f)
					break;
				float dirX = velocityX / speed;
				float dirZ = velocityZ / speed;

				float newPositionX = positionX + dirX;
				float newPositionZ = positionZ + dirZ;

				// 영역 밖 — 남은 sediment 버리고 종료 (다음 region 으로 흐를 수 있지만 1차 X).
				if (newPositionX < 0f || newPositionX >= width - 1 || newPositionZ < 0f || newPositionZ >= height - 1)
					break;

				int newCellX = (int)newPositionX;
				int newCellZ = (int)newPositionZ;
				float newFracX = newPositionX - newCellX;
				float newFracZ = newPositionZ - newCellZ;

				float newHeight = SampleHeightBilinear(heightmap, width, height, newCellX, newCellZ, newFracX, newFracZ);
				float deltaHeight = newHeight - hag.height;

				// sediment capacity — 가파른 내리막 + 빠른 + 물 많을수록 더 운반.
				float sedimentCapacity = Mathf.Max(
					-deltaHeight * speed * water * parameters.sedimentCapacityFactor,
					parameters.minSedimentCapacity);

				if (sediment > sedimentCapacity || deltaHeight > 0f)
				{
					// Deposit. 오르막 (deltaHeight > 0) 시 deltaHeight 까지만 (구덩이 메움). 평소 (sediment - capacity) * depositRate.
					float amountToDeposit = (deltaHeight > 0f)
						? Mathf.Min(deltaHeight, sediment)
						: (sediment - sedimentCapacity) * parameters.depositRate;
					sediment -= amountToDeposit;
					AddBilinear(heightmap, width, height, cellX, cellZ, fracX, fracZ, amountToDeposit);
				}
				else
				{
					// Erode. -deltaHeight 한계 (홀 방지 — 새 위치보다 깊게 못 깎음).
					float amountToErode = Mathf.Min(
						(sedimentCapacity - sediment) * parameters.erosionRate,
						-deltaHeight);
					sediment += amountToErode;
					AddBilinear(heightmap, width, height, cellX, cellZ, fracX, fracZ, -amountToErode);
				}

				water *= (1f - parameters.evaporRate);
				if (water < 0.0001f)
					break;

				positionX = newPositionX;
				positionZ = newPositionZ;
			}
		}

		private struct HeightAndGradient
		{
			public float height;
			public float gradientX;
			public float gradientZ;
		}

		/// <summary>
		/// (cellX+fracX, cellZ+fracZ) 위치의 bilinear height + gradient.
		/// gradient = (h(x+1,z) - h(x,z)) 식의 부드러운 보간.
		/// </summary>
		private static HeightAndGradient SampleHeightAndGradient(
			float[,] heightmap, int width, int height,
			int cellX, int cellZ, float fracX, float fracZ)
		{
			int x0 = Mathf.Clamp(cellX, 0, width - 1);
			int z0 = Mathf.Clamp(cellZ, 0, height - 1);
			int x1 = Mathf.Clamp(cellX + 1, 0, width - 1);
			int z1 = Mathf.Clamp(cellZ + 1, 0, height - 1);

			float h00 = heightmap[x0, z0];
			float h10 = heightmap[x1, z0];
			float h01 = heightmap[x0, z1];
			float h11 = heightmap[x1, z1];

			HeightAndGradient hag = default;
			hag.height = h00 * (1f - fracX) * (1f - fracZ) + h10 * fracX * (1f - fracZ) + h01 * (1f - fracX) * fracZ + h11 * fracX * fracZ;
			hag.gradientX = (h10 - h00) * (1f - fracZ) + (h11 - h01) * fracZ;
			hag.gradientZ = (h01 - h00) * (1f - fracX) + (h11 - h10) * fracX;
			return hag;
		}

		private static float SampleHeightBilinear(
			float[,] heightmap, int width, int height,
			int cellX, int cellZ, float fracX, float fracZ)
		{
			int x0 = Mathf.Clamp(cellX, 0, width - 1);
			int z0 = Mathf.Clamp(cellZ, 0, height - 1);
			int x1 = Mathf.Clamp(cellX + 1, 0, width - 1);
			int z1 = Mathf.Clamp(cellZ + 1, 0, height - 1);

			float h00 = heightmap[x0, z0];
			float h10 = heightmap[x1, z0];
			float h01 = heightmap[x0, z1];
			float h11 = heightmap[x1, z1];
			return h00 * (1f - fracX) * (1f - fracZ) + h10 * fracX * (1f - fracZ) + h01 * (1f - fracX) * fracZ + h11 * fracX * fracZ;
		}

		/// <summary>
		/// (cellX+fracX, cellZ+fracZ) 위치에 amount 를 bilinear 로 4 cell 분배.
		/// erode/deposit 양쪽 사용 (음수 amount = 깎임).
		/// </summary>
		private static void AddBilinear(
			float[,] heightmap, int width, int height,
			int cellX, int cellZ, float fracX, float fracZ, float amount)
		{
			int x0 = Mathf.Clamp(cellX, 0, width - 1);
			int z0 = Mathf.Clamp(cellZ, 0, height - 1);
			int x1 = Mathf.Clamp(cellX + 1, 0, width - 1);
			int z1 = Mathf.Clamp(cellZ + 1, 0, height - 1);

			heightmap[x0, z0] += amount * (1f - fracX) * (1f - fracZ);
			heightmap[x1, z0] += amount * fracX * (1f - fracZ);
			heightmap[x0, z1] += amount * (1f - fracX) * fracZ;
			heightmap[x1, z1] += amount * fracX * fracZ;
		}
	}
}
