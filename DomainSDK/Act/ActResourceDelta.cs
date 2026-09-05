namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 한 행동이 자원 하나를 얼마나 바꾸는가 (TASK-WM-408). 순수 값 타입 (DomainSDK).
    /// 부호 규약은 <see cref="ActNeedDelta"/> 와 같다 — 음수 = 소모(씨앗을 심으면 씨앗이 준다),
    /// 양수 = 생성(수확하면 작물이 는다).
    ///
    /// <see cref="ResourceId"/> 는 enum 이 아니라 데이터 주도 id 라 모드·UGC 가 만든 자원도
    /// 같은 원장을 그대로 탄다 — 새 자원이 생겨도 코어는 안 바뀐다.
    /// </summary>
    public readonly struct ActResourceDelta
    {
        public readonly ResourceId Resource;

        /// <summary>수량 변화. 음수 = 소모, 양수 = 생성.</summary>
        public readonly int Amount;

        public ActResourceDelta(ResourceId resource, int amount)
        {
            Resource = resource;
            Amount = amount;
        }

        public bool IsCost => Amount < 0;

        public override string ToString() => $"{Resource}{(Amount >= 0 ? "+" : string.Empty)}{Amount}";
    }
}
