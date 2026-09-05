using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Mathf 의 엔진 무관 대응물 (TASK-WM-214).
	/// DomainSDK 가 Unity 프로세스 밖(헤드리스 서버 / 웹 백엔드)에서도 같은 판정을 내려야 하므로,
	/// 시뮬 계산에 쓰이는 Mathf 표면만 <b>같은 이름 · 같은 수치 거동</b>으로 다시 세운다.
	/// 이름을 그대로 두는 이유 = 호출부 수정 0 (using 한 줄만 바뀐다).
	/// </summary>
	public static class Mathf
	{
		public const float PI = (float)Math.PI;
		public const float Epsilon = float.Epsilon;
		public const float Deg2Rad = PI * 2f / 360f;
		public const float Rad2Deg = 360f / (PI * 2f);

		public static float Abs(float value) => Math.Abs(value);
		public static int Abs(int value) => Math.Abs(value);

		public static float Max(float a, float b) => a > b ? a : b;
		public static int Max(int a, int b) => a > b ? a : b;
		public static float Min(float a, float b) => a < b ? a : b;
		public static int Min(int a, int b) => a < b ? a : b;

		public static float Clamp(float value, float min, float max)
		{
			if (value < min)
			{
				return min;
			}

			if (value > max)
			{
				return max;
			}

			return value;
		}

		public static int Clamp(int value, int min, int max)
		{
			if (value < min)
			{
				return min;
			}

			if (value > max)
			{
				return max;
			}

			return value;
		}

		public static float Clamp01(float value)
		{
			if (value < 0f)
			{
				return 0f;
			}

			if (value > 1f)
			{
				return 1f;
			}

			return value;
		}

		public static float Floor(float value) => (float)Math.Floor(value);
		public static float Ceil(float value) => (float)Math.Ceiling(value);
		public static float Round(float value) => (float)Math.Round(value);

		public static int FloorToInt(float value) => (int)Math.Floor(value);
		public static int CeilToInt(float value) => (int)Math.Ceiling(value);

		/// <summary>Unity 와 동일하게 <b>은행가 반올림</b>(중간값은 짝수로) — 0.5 → 0, 1.5 → 2.</summary>
		public static int RoundToInt(float value) => (int)Math.Round(value);

		public static float Sqrt(float value) => (float)Math.Sqrt(value);
		public static float Pow(float value, float power) => (float)Math.Pow(value, power);
		public static float Sin(float radians) => (float)Math.Sin(radians);
		public static float Cos(float radians) => (float)Math.Cos(radians);
		public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);

		public static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));
		public static float LerpUnclamped(float a, float b, float t) => a + ((b - a) * t);

		public static float InverseLerp(float a, float b, float value)
		{
			if (Approximately(a, b))
			{
				return 0f;
			}

			return Clamp01((value - a) / (b - a));
		}

		/// <summary>0 이상 length 미만으로 되감는다 (음수 입력도 양수 구간으로).</summary>
		public static float Repeat(float t, float length)
		{
			return Clamp(t - (Floor(t / length) * length), 0f, length);
		}

		/// <summary>두 각(도) 사이의 최단 차이 — -180 ~ 180.</summary>
		public static float DeltaAngle(float current, float target)
		{
			float delta = Repeat(target - current, 360f);
			if (delta > 180f)
			{
				delta -= 360f;
			}

			return delta;
		}

		public static bool Approximately(float a, float b)
		{
			return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
		}

		public static float Sign(float value) => value >= 0f ? 1f : -1f;
	}
}
