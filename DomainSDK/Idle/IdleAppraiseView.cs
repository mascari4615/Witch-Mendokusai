using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>감정 한 줄이 화면에 보이는 값과 현재 차단 사유.</summary>
    public readonly struct IdleAppraiseView
    {
        public IdleAppraiseView(double cost, AppraiseBlock block)
        {
            Cost = cost;
            Block = block;
        }

        public double Cost { get; }
        public AppraiseBlock Block { get; }
    }
}


