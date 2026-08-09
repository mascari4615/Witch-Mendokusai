using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Vector3 의 엔진 무관 대응물 (TASK-WM-214).
	/// </summary>
	[Serializable]
	public struct Vector3 : IEquatable<Vector3>
	{
		public const float kEpsilon = 1E-05f;
		public const float kEpsilonNormalSqrt = 1E-15f;

		public float x;
		public float y;
		public float z;

		public Vector3(float x, float y)
		{
			this.x = x;
			this.y = y;
			z = 0f;
		}

		public Vector3(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static Vector3 zero => new Vector3(0f, 0f, 0f);
		public static Vector3 one => new Vector3(1f, 1f, 1f);
		public static Vector3 up => new Vector3(0f, 1f, 0f);
		public static Vector3 down => new Vector3(0f, -1f, 0f);
		public static Vector3 left => new Vector3(-1f, 0f, 0f);
		public static Vector3 right => new Vector3(1f, 0f, 0f);
		public static Vector3 forward => new Vector3(0f, 0f, 1f);
		public static Vector3 back => new Vector3(0f, 0f, -1f);

		public float magnitude => Mathf.Sqrt((x * x) + (y * y) + (z * z));
		public float sqrMagnitude => (x * x) + (y * y) + (z * z);

		public Vector3 normalized => Normalize(this);

		public float this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return x;
					case 1: return y;
					case 2: return z;
					default: throw new IndexOutOfRangeException($"Invalid Vector3 index addressed: {index}!");
				}
			}
			set
			{
				switch (index)
				{
					case 0: x = value; break;
					case 1: y = value; break;
					case 2: z = value; break;
					default: throw new IndexOutOfRangeException($"Invalid Vector3 index addressed: {index}!");
				}
			}
		}

		public void Set(float newX, float newY, float newZ)
		{
			x = newX;
			y = newY;
			z = newZ;
		}

		public void Normalize()
		{
			this = Normalize(this);
		}

		public static Vector3 Normalize(Vector3 value)
		{
			float length = value.magnitude;
			if (length > kEpsilon)
			{
				return value / length;
			}

			return zero;
		}

		public static float Distance(Vector3 a, Vector3 b)
		{
			float deltaX = a.x - b.x;
			float deltaY = a.y - b.y;
			float deltaZ = a.z - b.z;
			return Mathf.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
		}

		public static float Dot(Vector3 lhs, Vector3 rhs) => (lhs.x * rhs.x) + (lhs.y * rhs.y) + (lhs.z * rhs.z);

		public static Vector3 Cross(Vector3 lhs, Vector3 rhs)
		{
			return new Vector3(
				(lhs.y * rhs.z) - (lhs.z * rhs.y),
				(lhs.z * rhs.x) - (lhs.x * rhs.z),
				(lhs.x * rhs.y) - (lhs.y * rhs.x));
		}

		public static float Angle(Vector3 from, Vector3 to)
		{
			float denominator = Mathf.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
			if (denominator < kEpsilonNormalSqrt)
			{
				return 0f;
			}

			float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
			return (float)Math.Acos(dot) * Mathf.Rad2Deg;
		}

		public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
		{
			t = Mathf.Clamp01(t);
			return new Vector3(
				a.x + ((b.x - a.x) * t),
				a.y + ((b.y - a.y) * t),
				a.z + ((b.z - a.z) * t));
		}

		public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
		{
			return new Vector3(
				a.x + ((b.x - a.x) * t),
				a.y + ((b.y - a.y) * t),
				a.z + ((b.z - a.z) * t));
		}

		public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
		{
			Vector3 delta = target - current;
			float distance = delta.magnitude;
			if (distance <= maxDistanceDelta || distance < kEpsilon)
			{
				return target;
			}

			return current + (delta / distance * maxDistanceDelta);
		}

		public static Vector3 Project(Vector3 vector, Vector3 onNormal)
		{
			float sqrMagnitude = Dot(onNormal, onNormal);
			if (sqrMagnitude < Mathf.Epsilon)
			{
				return zero;
			}

			return onNormal * (Dot(vector, onNormal) / sqrMagnitude);
		}

		public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
		{
			return vector - Project(vector, planeNormal);
		}

		public static Vector3 Min(Vector3 lhs, Vector3 rhs)
		{
			return new Vector3(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y), Mathf.Min(lhs.z, rhs.z));
		}

		public static Vector3 Max(Vector3 lhs, Vector3 rhs)
		{
			return new Vector3(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y), Mathf.Max(lhs.z, rhs.z));
		}

		public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

		public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
		{
			float sqrMagnitude = vector.sqrMagnitude;
			if (sqrMagnitude > maxLength * maxLength)
			{
				float length = Mathf.Sqrt(sqrMagnitude);
				return vector / length * maxLength;
			}

			return vector;
		}

		public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
		public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
		public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
		public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
		public static Vector3 operator *(float a, Vector3 b) => new Vector3(a * b.x, a * b.y, a * b.z);
		public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x / b, a.y / b, a.z / b);

		public static bool operator ==(Vector3 lhs, Vector3 rhs) => (lhs - rhs).sqrMagnitude < kEpsilon * kEpsilon;
		public static bool operator !=(Vector3 lhs, Vector3 rhs) => (lhs == rhs) == false;

		public bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;

		public override bool Equals(object other)
		{
			if (other is Vector3 == false)
			{
				return false;
			}

			return Equals((Vector3)other);
		}

		public override int GetHashCode()
		{
			return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
		}

		public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";

		public static implicit operator Vector3(Vector3Int value) => new Vector3(value.x, value.y, value.z);

		public static implicit operator Vector3(Vector2 value) => new Vector3(value.x, value.y, 0f);

		public static implicit operator Vector2(Vector3 value) => new Vector2(value.x, value.y);

#if UNITY_5_3_OR_NEWER
		public static implicit operator UnityEngine.Vector3(Vector3 value) => new UnityEngine.Vector3(value.x, value.y, value.z);
		// ★ 방향이 다르다 (TASK-WM-214): 판정 -> 엔진 은 암시적, 엔진 -> 판정 은 **명시적**.
		//   양방향 암시로 두면 두 타입을 한 식에서 섞을 때 연산자가 모호해진다(CS0034).
		//   더 큰 이유: 시뮬이 정본이고 엔진은 그 표현이다. 엔진 값이 시뮬로 들어오는 자리는
		//   드물고 중요하므로 캐스트로 눈에 보이게 둔다 - 「여기서 판정 세계로 들어간다」.
		public static explicit operator Vector3(UnityEngine.Vector3 value) => new Vector3(value.x, value.y, value.z);
#endif
	}
}
