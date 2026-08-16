using System;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 밭 한 칸이 있는 자리 (TASK-WM-410) — 블록 좌표. 순수 값 타입 (DomainSDK, 엔진 무관).
    ///
    /// ★ 왜 새 좌표계를 안 만드나: WM 엔 이미 복셀이 있다(청크 16×64×16, `VoxelConstants`).
    ///   땅이 이미 격자인데 밭만 다른 격자를 쓰면 두 격자를 평생 맞춰야 한다.
    ///   그래서 이건 <b>새 좌표계가 아니라 블록 좌표를 그대로 담는 그릇</b>이다 —
    ///   엔진 타입(Vector3Int)을 안 쓰는 이유는 DomainSDK 가 엔진을 몰라야 하기 때문뿐이다.
    ///
    /// 옛 칸 번호(plotId, 좌표 없는 정수)는 <see cref="Legacy"/> 로 담는다 — 이미 저장된 온실이
    /// 좌표를 얻을 때까지 같은 자리에 계속 서 있게 하는 다리다.
    /// </summary>
    [Serializable]
    public readonly struct FarmCoord : IEquatable<FarmCoord>, IComparable<FarmCoord>
    {
        // 좌표 없는 옛 칸이 사는 높이 — 실제 땅(y >= 0)과 안 겹치도록 아래에 둔다.
        public const int LEGACY_Y = -1;

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public FarmCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 월드 위치가 속한 블록 자리 (TASK-WM-410) — 내림(floor)이지 반올림이 아니다.
        /// 반올림하면 x=-0.2 가 0 이 되어 <b>음수 쪽 땅이 한 칸씩 밀린다</b>(원점 근처에서만 티가 나서 늦게 잡힌다).
        /// 엔진 타입을 안 받는 이유는 DomainSDK 가 엔진을 몰라야 하기 때문뿐이다.
        /// </summary>
        public static FarmCoord FromWorld(float x, float y, float z)
        {
            return new FarmCoord(FloorToInt(x), FloorToInt(y), FloorToInt(z));
        }

        private static int FloorToInt(float value)
        {
            int truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }

        /// <summary>좌표 없던 옛 칸 번호를 자리로 바꾼다(마이그레이션 다리). 번호가 같으면 자리도 같다.</summary>
        public static FarmCoord Legacy(int plotId) => new FarmCoord(plotId, LEGACY_Y, 0);

        /// <summary>아직 진짜 땅에 못 박힌 옛 칸인가.</summary>
        public bool IsLegacy => Y == LEGACY_Y;

        /// <summary>옛 칸 번호 — 진짜 좌표를 얻은 칸엔 의미 없다(호환 경로 전용).</summary>
        public int LegacyPlotId => X;

        public bool Equals(FarmCoord other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is FarmCoord other && Equals(other);

        public override int GetHashCode() => unchecked((X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791));

        /// <summary>결정적 순회용 정렬 — (Y, Z, X) 순. 같은 밭은 어느 기계에서도 같은 순서로 돌아간다.</summary>
        public int CompareTo(FarmCoord other)
        {
            if (Y != other.Y)
            {
                return Y.CompareTo(other.Y);
            }

            if (Z != other.Z)
            {
                return Z.CompareTo(other.Z);
            }

            return X.CompareTo(other.X);
        }

        public override string ToString() => IsLegacy ? $"Plot({LegacyPlotId})" : $"({X},{Y},{Z})";
    }
}
