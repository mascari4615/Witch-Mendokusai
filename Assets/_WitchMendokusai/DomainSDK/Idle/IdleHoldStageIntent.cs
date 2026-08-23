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

    /// <summary>
    /// 「저 단계로 간다」 — 이미 지나온 자리로 <b>물러난다</b> (TASK-WM-406).
    ///
    /// ★ 이게 없으면 벽에서 게임이 <b>완전히 멎는다</b>. 못 잡으니 자원이 0 이고,
    ///   자원이 0 이니 올릴 수도 없다 — 실측(이레 시뮬)에서 판마다 2시간이 통째로
    ///   「아무 일도 안 일어남」이었던 이유가 이것이다.
    ///   난이도를 완만하게 해도(1.55 → 1.08) 똑같았다. 손잡이 문제가 아니라 <b>없는 기능</b>이었다.
    ///
    /// ★ 깊이 밀기 계열이 같은 자리에 같은 것을 둔다 — 막히면 지나온 구역으로 돌아가 벌고 다시 민다.
    ///   「어디서 사냥할까」가 진짜 결정이 되려면 <b>앞뿐 아니라 뒤로도</b> 갈 수 있어야 한다.
    /// </summary>
    public readonly struct IdleGoToStageIntent : IGameIntent
    {
        public int Stage { get; }

        public IdleGoToStageIntent(int stage)
        {
            Stage = stage;
        }
    }
}
