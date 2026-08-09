using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// UnityEngine.Vector2Int 의 엔진 무관 대응물 (TASK-WM-214).
	/// 격자 좌표는 DomainSDK 판정의 뼈대라 엔진 밖에서도 같은 값·같은 해시로 동작해야 한다.
	/// 이름·필드명(x, y)을 Unity 와 똑같이 두어 호출부는 using 한 줄만 바뀐다.
	/// Unity 안에서는 아래 암시적 변환으로 UnityEngine 타입과 그대로 섞인다.
	/// </summary>
	[Serializable]
	public struct Vector2Int : IEquatable<Vector2Int>
	{
		public int x;
		public int y;

		public Vector2Int(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public static Vector2Int zero => new Vector2Int(0, 0);
		public static Vector2Int one => new Vector2Int(1, 1);
		public static Vector2Int up => new Vector2Int(0, 1);
		public static Vector2Int down => new Vector2Int(0, -1);
		public static Vector2Int left => new Vector2Int(-1, 0);
		public static Vector2Int right => new Vector2Int(1, 0);

		public float magnitude => Mathf.Sqrt((x * x) + (y * y));
		public int sqrMagnitude => (x * x) + (y * y);

		public int this[int index]
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

				throw new IndexOutOfRangeException($"Invalid Vector2Int index addressed: {index}!");
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

				throw new IndexOutOfRangeException($"Invalid Vector2Int index addressed: {index}!");
			}
		}

		public void Set(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public static float Distance(Vector2Int a, Vector2Int b)
		{
			float deltaX = a.x - b.x;
			float deltaY = a.y - b.y;
			return Mathf.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
		}

		public static Vector2Int Min(Vector2Int lhs, Vector2Int rhs)
		{
			return new Vector2Int(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y));
		}

		public static Vector2Int Max(Vector2Int lhs, Vector2Int rhs)
		{
			return new Vector2Int(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y));
		}

		public static Vector2Int Scale(Vector2Int a, Vector2Int b) => new Vector2Int(a.x * b.x, a.y * b.y);

		public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
		public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
		public static Vector2Int operator -(Vector2Int a) => new Vector2Int(-a.x, -a.y);
		public static Vector2Int operator *(Vector2Int a, int b) => new Vector2Int(a.x * b, a.y * b);
		public static Vector2Int operator *(int a, Vector2Int b) => new Vector2Int(a * b.x, a * b.y);
		public static Vector2Int operator *(Vector2Int a, Vector2Int b) => new Vector2Int(a.x * b.x, a.y * b.y);
		public static Vector2Int operator /(Vector2Int a, int b) => new Vector2Int(a.x / b, a.y / b);

		public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y;
		public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => (lhs == rhs) == false;

		public bool Equals(Vector2Int other) => x == other.x && y == other.y;

		public override bool Equals(object other)
		{
			if (other is Vector2Int == false)
			{
				return false;
			}

			return Equals((Vector2Int)other);
		}

		/// <summary>Unity 와 같은 조합식 — 같은 격자 키가 같은 버킷에 떨어진다.</summary>
		public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

		public override string ToString() => $"({x}, {y})";

#if UNITY_5_3_OR_NEWER
		public static implicit operator UnityEngine.Vector2Int(Vector2Int value) => new UnityEngine.Vector2Int(value.x, value.y);
		public static implicit operator Vector2Int(UnityEngine.Vector2Int value) => new Vector2Int(value.x, value.y);
		public static implicit operator UnityEngine.Vector2(Vector2Int value) => new UnityEngine.Vector2(value.x, value.y);
#endif
	}
}
