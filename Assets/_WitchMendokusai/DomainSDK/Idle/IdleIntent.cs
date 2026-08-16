using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 사람이 방치 판에서 <b>하려는 것</b> (TASK-WM-406).
    ///
    /// ★ 「버튼을 눌렀다」가 아니라 「올리려 한다」다 — 버튼이든 단축키든 자동 매크로든
    ///   시험 코드든 같은 문으로 모인다. 그래서 표현을 통째로 갈아도 조작이 그대로 살고,
    ///   글자 표현으로 돌리는 헤드리스 검증이 사람과 <b>같은 길</b>을 밟는다.
    ///
    /// ★ 받아들여질지는 코어가 정한다 — 자원이 모자라면 아무 일도 안 일어난다.
    ///   버튼을 흐리게 하는 건 친절이지 규칙이 아니다.
    /// </summary>
    public readonly struct IdleRaiseUpgradeIntent : IGameIntent
    {
        public IdleUpgradeKind Kind { get; }

        public IdleRaiseUpgradeIntent(IdleUpgradeKind kind)
        {
            Kind = kind;
        }
    }
}
