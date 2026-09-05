namespace WitchMendokusai.DomainSDK.Discovery
{
    /// <summary>
    /// 한 갈래를 얼마나 채웠나 — 전체 수와 그중 열린 수.
    ///
    /// 순수 값. 세는 쪽(화면)이 이미 답을 받아 둔 항목을 세어 만든다 — 등록소에 다시 묻지 않는다.
    /// </summary>
    public readonly struct DiscoveryProgress
    {
        public readonly int Total;
        public readonly int Unlocked;

        public DiscoveryProgress(int total, int unlocked)
        {
            Total = total;
            Unlocked = unlocked;
        }

        /// <summary>채운 비율 0..1. 전체가 0 이면 0 (0 나눗셈 방어).</summary>
        public double Ratio => Total <= 0 ? 0d : (double)Unlocked / Total;

        /// <summary>다 채웠나. 항목이 하나도 없으면 채운 것이 아니다.</summary>
        public bool IsComplete => Total > 0 && Unlocked >= Total;
    }
}
