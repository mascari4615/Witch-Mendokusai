using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>인형 하나의 전장 위치</summary>
    public readonly struct IdleFighterView
    {
        public IdleFighterView(int seat, double x, double y, double range, bool moving, long target)
        {
            Seat = seat;
            X = x;
            Y = y;
            Range = range;
            Moving = moving;
            Target = target;
        }

        public int Seat { get; }

        public double X { get; }

        public double Y { get; }

        /// <summary>사거리 (m)</summary>
        public double Range { get; }

        /// <summary>이번 틱에 걸었나</summary>
        public bool Moving { get; }

        /// <summary>노리는 적 번호. 없으면 -1</summary>
        public long Target { get; }
    }
}


