using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 「이 등급짜리 하나를 감정하겠다」 — 잠재가 붙어 나온다 (TASK-WM-406).
    ///
    /// ★ 이 게임에서 <b>사람이 주사위를 굴리는 유일한 자리</b>다.
    ///   시간이 굴리지 않는 이유는 <see cref="IdlePotentials"/> 에 적혀 있다.
    /// </summary>
    public readonly struct IdleAppraiseIntent : IGameIntent
    {
        /// <summary>감정할 장비의 등급.</summary>
        public int Tier { get; }

        public IdleAppraiseIntent(int tier)
        {
            Tier = tier;
        }
    }
}
