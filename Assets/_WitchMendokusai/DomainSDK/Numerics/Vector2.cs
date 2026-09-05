using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Vector2 의 엔진 무관 대응물 (TASK-WM-214).
	/// 부동소수 결과가 갈리면 서버와 클라의 판정이 갈리므로, 연산 순서까지 Unity 구현을 따른다.
	/// </summary>
	[Serializable]
	public struct Vector2 : IEquatable<Vector2>
	{
		public const float kEpsilon = 1E-05f;
		public const float kEpsilonNormalSqrt = 1E-15f;

		public float x;
		public float y;

		public Vector2(float x, float y)
		{
			this.x = x;
			this.y = y;
		}

		public static Vector2 zero => new Vector2(0f, 0f);
		public static Vector2 one => new Vector2(1f, 1f);
		public static Vector2 up => new Vector2(0f, 1f);
		public static Vector2 down => new Vector2(0f, -1f);
		public static Vector2 left => new Vector2(-1f, 0f);
		public static Vector2 right => new Vector2(1f, 0f);

		public float magnitude => Mathf.Sqrt((x * x) + (y * y));
		public float sqrMagnitude => (x * x) + (y * y);

		public Vector2 normalized
		{
			get
			{
				Vector2 result = new Vector2(x, y);
				result.Normalize();
				return result;
			}
		}

		public float this[int index]
		{
			get
			{
				if (index == 0)
				{
					return x;
				}

				if (index == 1)
				{
					return y;
				}

				throw new IndexOutOfRangeException($"Invalid Vector2 index addressed: {index}!");
			}
			set
			{
				if (index == 0)
				{
					x = value;
					return;
				}

				if (index == 1)
				{
					y = value;
					return;
				}

				throw new IndexOutOfRangeException($"Invalid Vector2 index addressed: {index}!");
			}
		}

		public void Set(float newX, float newY)
		{
			x = newX;
			y = newY;
		}

		public void Normalize()
		{
			float length = magnitude;
			if (length > kEpsilon)
			{
				this = this / length;
				return;
			}

			this = zero;
		}

		public static float Distance(Vector2 a, Vector2 b)
		{
			float deltaX = a.x - b.x;
			float deltaY = a.y - b.y;
			return Mathf.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
		}

		public static float Dot(Vector2 lhs, Vector2 rhs) => (lhs.x * rhs.x) + (lhs.y * rhs.y);

		/// <summary>두 벡터 사이의 각(도, 0~180). Unity 와 같은 클램프·엡실론.</summary>
		public static float Angle(Vector2 from, Vector2 to)
		{
			float denominator = Mathf.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
			if (denominator < kEpsilonNormalSqrt)
			{
				return 0f;
			}

			float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
			return (float)Math.Acos(dot) * Mathf.Rad2Deg;
		}

		/// <summary>from → to 의 부호 있는 각(도, -180~180). 반시계가 양수.</summary>
		public static float SignedAngle(Vector2 from, Vector2 to)
		{
			float unsigned = Angle(from, to);
			float sign = Mathf.Sign((from.x * to.y) - (from.y * to.x));
			return unsigned * sign;
		}

		public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
		{
			t = Mathf.Clamp01(t);
			return new Vector2(a.x + ((b.x - a.x) * t), a.y + ((b.y - a.y) * t));
		}

		public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
		{
			return new Vector2(a.x + ((b.x - a.x) * t), a.y + ((b.y - a.y) * t));
		}

		public static Vector2 Min(Vector2 lhs, Vector2 rhs) => new Vector2(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y));
		public static Vector2 Max(Vector2 lhs, Vector2 rhs) => new Vector2(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y));
		public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
		public static Vector2 Perpendicular(Vector2 inDirection) => new Vector2(-inDirection.y, inDirection.x);

		public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
		{
			float sqrMagnitude = vector.sqrMagnitude;
			if (sqrMagnitude > maxLength * maxLength)
			{
				float length = Mathf.Sqrt(sqrMagnitude);
				float normalizedX = vector.x / length;
				float normalizedY = vector.y / length;
				return new Vector2(normalizedX * maxLength, normalizedY * maxLength);
			}

			return vector;
		}

		public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
		public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
		public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
		public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
		public static Vector2 operator *(float a, Vector2 b) => new Vector2(a * b.x, a * b.y);
		public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
		public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.x / b, a.y / b);
		public static Vector2 operator /(Vector2 a, Vector2 b) => new Vector2(a.x / b.x, a.y / b.y);

		public static bool operator ==(Vector2 lhs, Vector2 rhs) => (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon;
		public static bool operator !=(Vector2 lhs, Vector2 rhs) => (lhs == rhs) == false;

		public bool Equals(Vector2 other) => x == other.x && y == other.y;

		public override bool Equals(object other)
		{
			if (other is Vector2 == false)
			{
				return false;
			}

			return Equals((Vector2)other);
		}

		public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

		public override string ToString() => $"({x:F2}, {y:F2})";

		public static implicit operator Vector2(Vector2Int value) => new Vector2(value.x, value.y);

	}
}
