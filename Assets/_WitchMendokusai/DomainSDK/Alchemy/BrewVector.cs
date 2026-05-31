using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 솥 지도(효과 공간) 위의 2D 벡터/좌표. 순수 데이터.
    /// UnityEngine 의존 0 (DomainSDK references=[] 정합 — UnityEngine.Vector2 미사용).
    /// 효과 공간 = 2D 평면. 재료 = 방향, 갈기 정도 = 이동 거리.
    /// </summary>
    [Serializable]
    public struct BrewVector
    {
        public float X;
        public float Y;

        public BrewVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static BrewVector Zero
        {
            get { return new BrewVector(0f, 0f); }
        }

        public float SqrMagnitude
        {
            get { return X * X + Y * Y; }
        }

        public float Magnitude
        {
            get { return (float)Math.Sqrt(SqrMagnitude); }
        }

        public static BrewVector operator +(BrewVector a, BrewVector b)
        {
            return new BrewVector(a.X + b.X, a.Y + b.Y);
        }

        public static BrewVector operator -(BrewVector a, BrewVector b)
        {
            return new BrewVector(a.X - b.X, a.Y - b.Y);
        }

        public static BrewVector operator *(BrewVector vector, float scale)
        {
            return new BrewVector(vector.X * scale, vector.Y * scale);
        }
    }
}
