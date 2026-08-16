using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 「다시 시작하겠다」 — 판을 접고 점수로 바꾼다 (TASK-WM-406).
    ///
    /// ★ 표현은 이 뜻만 보낸다. 될지 안 될지(<see cref="IdleTuning.PrestigeMinStage"/> 에 닿았나),
    ///   몇 점이 되는지는 전부 코어가 정한다 — 버튼이 규칙을 알면 표현마다 규칙이 갈린다.
    /// </summary>
    public readonly struct IdlePrestigeIntent : IGameIntent
    {
    }
}
