using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Color 의 엔진 무관 대응물 (TASK-WM-214).
	/// DomainSDK 안에서 색은 <b>데이터</b>다(히트맵 등급 등) — 렌더링은 Unity 쪽 책임.
	/// </summary>
	[Serializable]
	public struct Color : IEquatable<Color>
	{
		public float r;
		public float g;
		public float b;
		public float a;

		public Color(float r, float g, float b)
		{
			this.r = r;
			this.g = g;
			this.b = b;
			a = 1f;
		}

		public Color(float r, float g, float b, float a)
		{
			this.r = r;
			this.g = g;
			this.b = b;
			this.a = a;
		}

		public static Color clear => new Color(0f, 0f, 0f, 0f);
		public static Color black => new Color(0f, 0f, 0f, 1f);
		public static Color white => new Color(1f, 1f, 1f, 1f);
		public static Color red => new Color(1f, 0f, 0f, 1f);
		public static Color green => new Color(0f, 1f, 0f, 1f);
		public static Color blue => new Color(0f, 0f, 1f, 1f);
		public static Color yellow => new Color(1f, 0.9215686f, 0.01568628f, 1f);
		public static Color cyan => new Color(0f, 1f, 1f, 1f);
		public static Color magenta => new Color(1f, 0f, 1f, 1f);
		public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);

		public float this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return r;
					case 1: return g;
					case 2: return b;
					case 3: return a;
					default: throw new IndexOutOfRangeException($"Invalid Color index addressed: {index}!");
				}
			}
			set
			{
				switch (index)
				{
					case 0: r = value; break;
					case 1: g = value; break;
					case 2: b = value; break;
					case 3: a = value; break;
					default: throw new IndexOutOfRangeException($"Invalid Color index addressed: {index}!");
				}
			}
		}

		public static Color Lerp(Color a, Color b, float t)
		{
			t = Mathf.Clamp01(t);
			return new Color(
				a.r + ((b.r - a.r) * t),
				a.g + ((b.g - a.g) * t),
				a.b + ((b.b - a.b) * t),
				a.a + ((b.a - a.a) * t));
		}

		public static Color LerpUnclamped(Color a, Color b, float t)
		{
			return new Color(
				a.r + ((b.r - a.r) * t),
				a.g + ((b.g - a.g) * t),
				a.b + ((b.b - a.b) * t),
				a.a + ((b.a - a.a) * t));
		}

		public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
		public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
		public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
		public static Color operator *(Color a, float b) => new Color(a.r * b, a.g * b, a.b * b, a.a * b);
		public static Color operator *(float b, Color a) => new Color(a.r * b, a.g * b, a.b * b, a.a * b);
		public static Color operator /(Color a, float b) => new Color(a.r / b, a.g / b, a.b / b, a.a / b);

		public static bool operator ==(Color lhs, Color rhs)
		{
			return lhs.r == rhs.r && lhs.g == rhs.g && lhs.b == rhs.b && lhs.a == rhs.a;
		}

		public static bool operator !=(Color lhs, Color rhs) => (lhs == rhs) == false;

		public bool Equals(Color other) => this == other;

		public override bool Equals(object other)
		{
			if (other is Color == false)
			{
				return false;
			}

			return Equals((Color)other);
		}

		public override int GetHashCode()
		{
			return r.GetHashCode() ^ (g.GetHashCode() << 2) ^ (b.GetHashCode() >> 2) ^ (a.GetHashCode() >> 1);
		}

		public override string ToString() => $"RGBA({r:F3}, {g:F3}, {b:F3}, {a:F3})";

#if UNITY_5_3_OR_NEWER
		public static implicit operator UnityEngine.Color(Color value) => new UnityEngine.Color(value.r, value.g, value.b, value.a);
		public static implicit operator Color(UnityEngine.Color value) => new Color(value.r, value.g, value.b, value.a);
#endif
	}
}
