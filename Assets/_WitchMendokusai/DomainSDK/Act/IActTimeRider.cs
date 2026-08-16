namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 시간을 타고 변하는 것들 (TASK-WM-408) — 자라는 작물, 배고파지는 몸, 식는 가마솥.
    ///
    /// ★ 왜 원장 밖의 별도 개념인가: 원장은 <b>선언한 것만</b> 건다(<see cref="ActLedger"/>).
    ///   그런데 「밭을 가는 한 시간 동안 작물도 한 시간 자란다」는 그 행동이 선언한 게 아니라
    ///   <b>시간이 흘렀기 때문에</b> 일어나는 일이다. 이걸 행동마다 적으면 행동 수만큼 어긋난다.
    ///
    /// ★ 왜 강제가 아닌가: 태우지 않으면 아무도 안 탄다. 시계를 안 보는 게임(오락실 캐비닛)은
    ///   이 자리를 비워 두면 되고, 그때 세계는 시간이 흘러도 아무 일도 안 겪는다.
    /// </summary>
    public interface IActTimeRider
    {
        /// <summary>시간이 이만큼 흘렀다. <paramref name="dayChanged"/> = 이 사이에 자정을 넘었다.</summary>
        void RideMinutes(int minutes, bool dayChanged);
    }
}
