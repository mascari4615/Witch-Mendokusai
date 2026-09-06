using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 생산자 하나가 화면에 보이는 모습 — 몇 개 · 값 · 초당 · 살 수 있나 · 아직 숨길까.
    /// </summary>
    public readonly struct IdleProducerView
    {
        public int Kind { get; }
        public long Owned { get; }

        /// <summary>한 개 더 살 때의 값.</summary>
        public double NextCost { get; }

        /// <summary>이 종류가 지금 내고 있는 초당 자원.</summary>
        public double OutputTotal { get; }

        public bool CanAfford { get; }

        /// <summary>아직 보여줄 때가 아니다 — 살 만해지기 직전에 나타난다.</summary>
        public bool Hidden { get; }

        /// <summary>
        /// 하나 더 사면 <b>판 전체 초당 수입</b>이 몇 배가 되나 (1.0 = 그대로).
        ///
        /// ★ 「이 줄이 지금 얼마를 내나」는 이미 보이지만, 정작 고를 때 필요한 건
        ///   <b>사고 나면 얼마나 좋아지나</b>다. 그게 없으면 값싼 것부터 누르는 것 말고 할 게 없다.
        /// </summary>
        public double IncomeGain { get; }

        /// <summary>지금 벌이로 몇 초 뒤에 살 수 있나 (0 = 지금).</summary>
        public double SecondsToAfford { get; }

        public IdleProducerView(int kind, long owned, double nextCost,
            double outputTotal, bool canAfford, bool hidden, double incomeGain, double secondsToAfford)
        {
            Kind = kind;
            Owned = owned;
            NextCost = nextCost;
            OutputTotal = outputTotal;
            CanAfford = canAfford;
            Hidden = hidden;
            IncomeGain = incomeGain;
            SecondsToAfford = secondsToAfford;
        }
    }
}


