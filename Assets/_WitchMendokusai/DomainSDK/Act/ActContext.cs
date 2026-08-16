using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 행동이 걸리는 대상 — 누구의 몸이고, 어느 창고이고, 어느 하늘인가 (TASK-WM-408).
    /// 순수 POCO (DomainSDK). 원장은 이 셋만 알고, 그 너머(지역·게임 종류·씬)는 모른다.
    ///
    /// 안 쓰는 자리는 비워 둘 수 있다 — 시계 없는 자리(캐비닛 게임)엔 달력이 없고,
    /// 몸이 없는 자리(순수 제작 시뮬)엔 욕구가 없다. 다만 <b>선언한 것은 있어야 한다</b>:
    /// 기운을 쓰겠다고 적어 놓고 몸이 없으면 그건 데이터가 틀린 것이라 조용히 넘기지 않는다.
    /// </summary>
    public sealed class ActContext
    {
        public ActContext(NeedState needs = null, NeedProfile needProfile = null, IActResourcePool resources = null, WorldCalendar calendar = null, IActTimeRider timeRider = null)
        {
            Needs = needs;
            NeedProfile = needProfile;
            Resources = resources;
            Calendar = calendar;
            TimeRider = timeRider;
        }

        /// <summary>행동하는 이의 몸 상태 (없을 수 있음).</summary>
        public NeedState Needs { get; }

        /// <summary>그 몸의 튜닝값 — 상한 클램프의 근거 (없을 수 있음).</summary>
        public NeedProfile NeedProfile { get; }

        /// <summary>자원을 꺼내고 담는 자리 (없을 수 있음).</summary>
        public IActResourcePool Resources { get; }

        /// <summary>세계의 하늘 — 행동이 먹은 시간만큼 흐른다 (없을 수 있음).</summary>
        public WorldCalendar Calendar { get; }

        /// <summary>흐른 시간을 타는 것들 — 자라는 작물·배고파지는 몸 (없을 수 있음 = 아무도 안 늙는다).</summary>
        public IActTimeRider TimeRider { get; }

        public bool HasBody => Needs != null && NeedProfile != null;
    }
}
