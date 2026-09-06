using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>적 하나의 전장 위치</summary>
    public readonly struct IdleFoeView
    {
        public IdleFoeView(long index, IdleFoeKind kind, bool boss, double x, double y, double healthRatio, double range)
        {
            Index = index;
            Kind = kind;
            Boss = boss;
            X = x;
            Y = y;
            HealthRatio = healthRatio;
            Range = range;
        }

        public long Index { get; }

        public IdleFoeKind Kind { get; }

        public bool Boss { get; }

        public double X { get; }

        public double Y { get; }

        public double HealthRatio { get; }

        public double Range { get; }
    }
}


