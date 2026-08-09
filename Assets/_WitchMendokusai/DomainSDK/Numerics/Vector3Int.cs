using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Vector3Int 의 엔진 무관 대응물 (TASK-WM-214).
	/// 건축 격자(GridData)의 사전 키로 쓰이므로 동등성·해시가 특히 중요하다.
	/// </summary>
	[Serializable]
	public struct Vector3Int : IEquatable<Vector3Int>
	{
		public int x;
		public int y;
		public int z;

		public Vector3Int(int x, int y)
		{
			this.x = x;
			this.y = y;
			z = 0;
		}

		public Vector3Int(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static Vector3Int zero => new Vector3Int(0, 0, 0);
		public static Vector3Int one => new Vector3Int(1, 1, 1);
		public static Vector3Int up => new Vector3Int(0, 1, 0);
		public static Vector3Int down => new Vector3Int(0, -1, 0);
		public static Vector3Int left => new Vector3Int(-1, 0, 0);
		public static Vector3Int right => new Vector3Int(1, 0, 0);
		public static Vector3Int forward => new Vector3Int(0, 0, 1);
		public static Vector3Int back => new Vector3Int(0, 0, -1);

		public float magnitude => Mathf.Sqrt((x * x) + (y * y) + (z * z));
		public int sqrMagnitude => (x * x) + (y * y) + (z * z);

		public int this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return x;
					case 1: return y;
					case 2: return z;
					default: throw new IndexOutOfRangeException($"Invalid Vector3Int index addressed: {index}!");
				}
			}
			set
			{
				switch (index)
				{
					case 0: x = value; break;
					case 1: y = value; break;
					case 2: z = value; break;
					default: throw new IndexOutOfRangeException($"Invalid Vector3Int index addressed: {index}!");
				}
			}
		}

		public void Set(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static float Distance(Vector3Int a, Vector3Int b) => (a - b).magnitude;

		/// <summary>실수 좌표를 아래로 내려 격자 칸으로 (Unity 와 같은 규칙).</summary>
		public static Vector3Int FloorToInt(Vector3 value)
		{
			return new Vector3Int(Mathf.FloorToInt(value.x), Mathf.FloorToInt(value.y), Mathf.FloorToInt(value.z));
		}

		public static Vector3Int CeilToInt(Vector3 value)
		{
			return new Vector3Int(Mathf.CeilToInt(value.x), Mathf.CeilToInt(value.y), Mathf.CeilToInt(value.z));
		}

		public static Vector3Int RoundToInt(Vector3 value)
		{
			return new Vector3Int(Mathf.RoundToInt(value.x), Mathf.RoundToInt(value.y), Mathf.RoundToInt(value.z));
		}

		public static Vector3Int Min(Vector3Int lhs, Vector3Int rhs)
		{
			return new Vector3Int(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y), Mathf.Min(lhs.z, rhs.z));
		}

		public static Vector3Int Max(Vector3Int lhs, Vector3Int rhs)
		{
			return new Vector3Int(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y), Mathf.Max(lhs.z, rhs.z));
		}

		public static Vector3Int Scale(Vector3Int a, Vector3Int b) => new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);

		public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
		public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);
		public static Vector3Int operator -(Vector3Int a) => new Vector3Int(-a.x, -a.y, -a.z);
		public static Vector3Int operator *(Vector3Int a, int b) => new Vector3Int(a.x * b, a.y * b, a.z * b);
		public static Vector3Int operator *(int a, Vector3Int b) => new Vector3Int(a * b.x, a * b.y, a * b.z);
		public static Vector3Int operator *(Vector3Int a, Vector3Int b) => new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);
		public static Vector3Int operator /(Vector3Int a, int b) => new Vector3Int(a.x / b, a.y / b, a.z / b);

		public static bool operator ==(Vector3Int lhs, Vector3Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
		public static bool operator !=(Vector3Int lhs, Vector3Int rhs) => (lhs == rhs) == false;

		public bool Equals(Vector3Int other) => x == other.x && y == other.y && z == other.z;

		public override bool Equals(object other)
		{
			if (other is Vector3Int == false)
			{
				return false;
			}

			return Equals((Vector3Int)other);
		}

		/// <summary>Unity 와 같은 조합식.</summary>
		public override int GetHashCode()
		{
			int yHash = y.GetHashCode();
			int zHash = z.GetHashCode();
			return x.GetHashCode() ^ (yHash << 4) ^ (yHash >> 28) ^ (zHash >> 4) ^ (zHash << 28);
		}

		public override string ToString() => $"({x}, {y}, {z})";

#if UNITY_5_3_OR_NEWER
		public static implicit operator UnityEngine.Vector3Int(Vector3Int value) => new UnityEngine.Vector3Int(value.x, value.y, value.z);
		// ★ 방향이 다르다 (TASK-WM-214): 판정 -> 엔진 은 암시적, 엔진 -> 판정 은 **명시적**.
		//   양방향 암시로 두면 두 타입을 한 식에서 섞을 때 연산자가 모호해진다(CS0034).
		//   더 큰 이유: 시뮬이 정본이고 엔진은 그 표현이다. 엔진 값이 시뮬로 들어오는 자리는
		//   드물고 중요하므로 캐스트로 눈에 보이게 둔다 - 「여기서 판정 세계로 들어간다」.
		public static explicit operator Vector3Int(UnityEngine.Vector3Int value) => new Vector3Int(value.x, value.y, value.z);
		public static implicit operator UnityEngine.Vector3(Vector3Int value) => new UnityEngine.Vector3(value.x, value.y, value.z);
#endif
	}
}
