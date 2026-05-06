using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 그리드 기반 Smooth filter — Box 3×3 평균을 N 회 반복.
	/// N pass = Gaussian 근사 (중심극한정리). N param 으로 무단계 강도 (1=약한 부드러움, 16=매우 부드러움).
	///
	/// 시각: erosion 결과의 noise 정리 + 강·골짜기 부드러운 윤곽.
	///
	/// 알고리즘:
	/// 1. ping-pong 두 버퍼 (input → output)
	/// 2. 각 cell 의 인접 3×3 cell (자신 포함, 경계는 가능한 것만) 평균 → output
	/// 3. swap (input, output) 후 다음 pass
	/// 4. iterations 회 반복 후 결과를 heightmap 에 복사 (홀수 pass 인 경우)
	///
	/// UnityEngine(Mathf, Tooltip/Range attribute) 의존. NodeGraph 의존 X.
	/// </summary>
	public static class SmoothGridSimulator
	{
		[Serializable]
		public struct Parameters
		{
			[Tooltip("iteration 수. 1=약한 부드러움, 16=매우 부드러움. 디폴트 4.")]
			[Range(1, 16)] public int iterations;

			public static Parameters Default => new()
			{
				iterations = 4,
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

			float[,] buffer = new float[width, height];
			float[,] source = heightmap;
			float[,] target = buffer;

			for (int pass = 0; pass < iterations; pass++)
			{
				for (int x = 0; x < width; x++)
				{
					for (int z = 0; z < height; z++)
					{
						target[x, z] = AverageBox3x3(source, width, height, x, z);
					}
				}

				// ping-pong swap
				float[,] swap = source;
				source = target;
				target = swap;
			}

			// 홀수 iteration 이면 결과는 buffer 에 — heightmap 으로 복사 필요
			if (source != heightmap)
			{
				for (int x = 0; x < width; x++)
				{
					for (int z = 0; z < height; z++)
					{
						heightmap[x, z] = source[x, z];
					}
				}
			}
		}

		private static float AverageBox3x3(float[,] source, int width, int height, int x, int z)
		{
			float sum = 0f;
			int count = 0;
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					int nx = x + dx;
					int nz = z + dz;
					if (nx < 0 || nx >= width || nz < 0 || nz >= height)
						continue;
					sum += source[nx, nz];
					count++;
				}
			}
			return sum / count;
		}
	}
}
