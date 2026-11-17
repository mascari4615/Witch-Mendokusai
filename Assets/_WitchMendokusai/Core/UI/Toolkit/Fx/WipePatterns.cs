using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 무늬 지도 함수. (x, y) 는 화면 비율 0~1 (위가 0), aspect 는 가로/세로
	/// 반환값이 작을수록 먼저 덮이고 먼저 걷힘. 크기는 MapWipe 가 0~1 로 맞추므로 순서만 의미
	/// </summary>
	public static class WipePatterns
	{
		public delegate float Pattern(float x, float y, float aspect);

		private const float TWO_PI = Mathf.PI * 2f;
		// 하트 곡선 좌표로 바꾸는 배율. 클수록 같은 진행에서 하트가 작음 (모양 정의, 조절은 진행 시간으로)
		private const float HEART_FIT = 2.6f;
		// 배율 이분 탐색 횟수. 14 회면 1/16000 정밀도
		private const int HEART_SEARCH_STEPS = 14;

		private static UIFxSettings Settings => UIFxSettings.Instance;

		/// <summary>원이 가장자리에서 가운데로 닫힘 (옛 CircleWipe)</summary>
		public static float Iris(float x, float y, float aspect) => -Radius(x, y, aspect);

		/// <summary>양옆 문이 가운데로 닫힘 (옛 Gate)</summary>
		public static float Gate(float x, float y, float aspect) => -Mathf.Abs(x - 0.5f);

		/// <summary>12시부터 시계 방향으로 쓸기</summary>
		public static float Clock(float x, float y, float aspect) => Angle01(x, y, aspect);

		/// <summary>바깥에서 안으로 감기는 나선</summary>
		public static float Spiral(float x, float y, float aspect)
		{
			float maxRadius = Mathf.Sqrt(aspect * aspect + 1f) * 0.5f;
			return Angle01(x, y, aspect) + Settings.wipeSpiralTurns * (1f - Radius(x, y, aspect) / maxRadius);
		}

		/// <summary>구름 무늬로 녹아 덮임</summary>
		public static float Dissolve(float x, float y, float aspect) => Fractal(x * aspect * Settings.wipeDissolveScale, y * Settings.wipeDissolveScale);

		/// <summary>물결 경계가 위에서 아래로</summary>
		public static float Wave(float x, float y, float aspect) => y + Settings.wipeWaveAmplitude * Mathf.Sin(x * TWO_PI * Settings.wipeWaveCount);

		/// <summary>별 모양으로 닫힘</summary>
		public static float Star(float x, float y, float aspect)
		{
			float theta = Angle01(x, y, aspect) * TWO_PI;
			float depth = Settings.wipeStarDepth;
			float edge = 1f - depth + depth * Mathf.Cos(Settings.wipeStarPoints * theta);
			return -Radius(x, y, aspect) / edge;
		}

		/// <summary>하트 모양으로 닫힘</summary>
		public static float Heart(float x, float y, float aspect)
		{
			// 하트 곡선 (x^2 + y^2 - 1)^3 - x^2 y^3 이 0 이하인 영역을 몇 배로 키워야 이 점이 경계에 닿는지. 클수록 바깥
			float offsetX = (x - 0.5f) * aspect * HEART_FIT;
			float offsetY = (0.5f - y) * HEART_FIT + 0.1f;
			float low = 0f;
			float high = 8f;
			for (int step = 0; step < HEART_SEARCH_STEPS; step++)
			{
				float middle = (low + high) * 0.5f;
				if (InsideHeart(offsetX / middle, offsetY / middle))
					high = middle;
				else
					low = middle;
			}
			return -high;
		}

		private static bool InsideHeart(float x, float y)
		{
			float squared = x * x + y * y - 1f;
			return squared * squared * squared - x * x * y * y * y <= 0f;
		}

		/// <summary>깨진 유리 조각이 하나씩 덮임</summary>
		public static float Crystal(float x, float y, float aspect)
		{
			int cells = Mathf.Max(2, Settings.wipeCrystalCells);
			Vector2 point = new(x * aspect, y);
			float nearest = float.MaxValue;
			int nearestIndex = 0;
			for (int i = 0; i < cells; i++)
			{
				Vector2 seed = new(Hash01(i, 11) * aspect, Hash01(i, 23));
				float distance = (seed - point).sqrMagnitude;
				if (distance < nearest)
				{
					nearest = distance;
					nearestIndex = i;
				}
			}
			float cellSize = Mathf.Sqrt(aspect / cells);
			return Hash01(nearestIndex, 37) * 0.85f + Mathf.Sqrt(nearest) / cellSize * 0.15f;
		}

		/// <summary>부채 날개들이 동시에 돌며 덮음</summary>
		public static float Fan(float x, float y, float aspect)
		{
			float blades = Mathf.Max(1, Settings.wipeFanBlades);
			float maxRadius = Mathf.Sqrt(aspect * aspect + 1f) * 0.5f;
			return Mathf.Repeat(Angle01(x, y, aspect) * blades, 1f) * 0.85f + (1f - Radius(x, y, aspect) / maxRadius) * 0.15f;
		}

		/// <summary>가운데에서 물결치며 퍼짐</summary>
		public static float Ripple(float x, float y, float aspect)
		{
			float radius = Radius(x, y, aspect);
			// 물결 폭이 커서 바깥 고리가 먼저 덮이는 구간이 생김. 고리 무늬로 의도
			return radius + Settings.wipeRippleAmplitude * Mathf.Sin(radius * Settings.wipeRippleFrequency);
		}

		/// <summary>사선 띠가 번갈아 반대 방향에서</summary>
		public static float Stripes(float x, float y, float aspect)
		{
			float across = (x * aspect + y) / (aspect + 1f) * Mathf.Max(1, Settings.wipeStripeCount);
			float along = (x * aspect - y + 1f) / (aspect + 1f);
			bool even = Mathf.FloorToInt(across) % 2 == 0;
			return even ? along : 1f - along;
		}

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

		private static float Radius(float x, float y, float aspect)
		{
			float offsetX = (x - 0.5f) * aspect;
			float offsetY = y - 0.5f;
			return Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
		}

		// 12시가 0, 시계 방향으로 1 까지
		private static float Angle01(float x, float y, float aspect)
		{
			float offsetX = (x - 0.5f) * aspect;
			float offsetY = y - 0.5f;
			return Mathf.Repeat(Mathf.Atan2(offsetX, -offsetY) / TWO_PI, 1f);
		}

		private static float Fractal(float x, float y)
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
