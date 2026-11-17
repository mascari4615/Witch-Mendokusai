using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>무늬 계산 공용 도구. 무늬 자체는 PatternUIWipe 상속 클래스 (Wipes 폴더)</summary>
	public static class WipePatterns
	{
		public const float TWO_PI = Mathf.PI * 2f;

		/// <summary>정수 둘을 0~1 의 고정 난수로</summary>
		public static float Hash01(int first, int second)
		{
			unchecked
			{
				uint hash = (uint)first * 73856093u ^ (uint)second * 19349663u;
				hash ^= hash >> 13;
				hash *= 0x5bd1e995u;
				hash ^= hash >> 15;
				return (hash & 0xFFFFFF) / (float)0x1000000;
			}
		}

		/// <summary>화면 가운데에서 거리 (세로 1 기준)</summary>
		public static float Radius(float x, float y, float aspect)
		{
			float offsetX = (x - 0.5f) * aspect;
			float offsetY = y - 0.5f;
			return Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
		}

		/// <summary>12시가 0, 시계 방향으로 1 까지</summary>
		public static float Angle01(float x, float y, float aspect)
		{
			float offsetX = (x - 0.5f) * aspect;
			float offsetY = y - 0.5f;
			return Mathf.Repeat(Mathf.Atan2(offsetX, -offsetY) / TWO_PI, 1f);
		}

		/// <summary>값 잡음 네 겹. 대략 0~1</summary>
		public static float Fractal(float x, float y)
		{
			float sum = 0f;
			float amplitude = 0.5f;
			for (int octave = 0; octave < 4; octave++)
			{
				sum += ValueNoise(x, y) * amplitude;
				x *= 2f;
				y *= 2f;
				amplitude *= 0.5f;
			}
			return sum;
		}

		private static float ValueNoise(float x, float y)
		{
			int cellX = Mathf.FloorToInt(x);
			int cellY = Mathf.FloorToInt(y);
			float fractionX = x - cellX;
			float fractionY = y - cellY;
			float smoothX = fractionX * fractionX * (3f - 2f * fractionX);
			float smoothY = fractionY * fractionY * (3f - 2f * fractionY);
			float top = Mathf.Lerp(Hash01(cellX, cellY), Hash01(cellX + 1, cellY), smoothX);
			float bottom = Mathf.Lerp(Hash01(cellX, cellY + 1), Hash01(cellX + 1, cellY + 1), smoothX);
			return Mathf.Lerp(top, bottom, smoothY);
		}
	}
}
