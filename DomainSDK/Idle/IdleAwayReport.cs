namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 자리를 비운 동안 <b>무슨 일이 있었나</b> (TASK-WM-406).
    ///
    /// ★ 돌아온 순간이 방치형의 보상이다. 그런데 화면은 「N 동안 잡아 뒀다」만 말했다 —
    ///   <b>얼마나</b>가 없으면 보상이 안 느껴지고, 방치형의 심장이 안 뛴다.
    ///
    /// ★ <b>흘린 시간</b>도 같이 말한다. 상한에 걸린 걸 안 알리면 사용자는 몇 시간을 흘린 줄도
    ///   모르고, 상한을 올릴 이유(환생)도 안 보인다. 손해는 조용하면 안 된다.
    /// </summary>
    public readonly struct IdleAwayReport
    {
        public IdleAwayReport(double askedSeconds, double creditedSeconds, bool hitCap,
            double capSeconds, double resourceGained, long killsGained, int stagesGained, int itemsGained)
        {
            AskedSeconds = askedSeconds;
            CreditedSeconds = creditedSeconds;
            HitCap = hitCap;
            CapSeconds = capSeconds;
            ResourceGained = resourceGained;
            KillsGained = killsGained;
            StagesGained = stagesGained;
            ItemsGained = itemsGained;
        }

        /// <summary>실제로 비운 시간(초).</summary>
        public double AskedSeconds { get; }

        /// <summary>그 중 쳐준 시간(초) — 상한에서 잘린다.</summary>
        public double CreditedSeconds { get; }

        /// <summary>상한에 걸렸나 — 걸렸으면 흘린 시간이 있다.</summary>
        public bool HitCap { get; }

        /// <summary>지금 상한(초).</summary>
        public double CapSeconds { get; }

        /// <summary>흘려버린 시간(초).</summary>
        public double LostSeconds => AskedSeconds > CreditedSeconds ? AskedSeconds - CreditedSeconds : 0d;

        public double ResourceGained { get; }
        public long KillsGained { get; }

        /// <summary>몇 단계나 나아갔나.</summary>
        public int StagesGained { get; }

        /// <summary>가방에 새로 들어온 장비 수.</summary>
        public int ItemsGained { get; }

        /// <summary>보여줄 것이 있나 — 없으면 화면이 아무 말도 안 한다.</summary>
        public bool HasAnything => CreditedSeconds > 0d;
    }
}
