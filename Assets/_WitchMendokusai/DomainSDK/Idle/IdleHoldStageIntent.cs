using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 「여기 머문다 / 계속 내려간다」 (TASK-WM-406).
    ///
    /// ★ 이 게임에서 사람이 하는 <b>둘째 종류의 결정</b>이다(첫째는 무엇을 올릴까).
    ///   얕으면 많이, 깊으면 좋은 것 — 어느 쪽을 원하는지는 코어가 정할 일이 아니다.
    /// </summary>
    public readonly struct IdleHoldStageIntent : IGameIntent
    {
        public bool Hold { get; }

        public IdleHoldStageIntent(bool hold)
        {
            Hold = hold;
        }
    }
}
