namespace WitchMendokusai.DomainSDK.Contracts
{
    /// <summary>
    /// 사람이 <b>하려는 것</b> — 표현이 코어에게 보내는 것. 마커 인터페이스다.
    ///
    /// ★ 「무엇을 눌렀나」가 아니라 「무엇을 하려 하나」다 — 버튼이든 단축키든 자동 스크립트든
    ///   같은 의도로 모인다. 그래야 표현을 갈아도 조작이 그대로 살고, 자동 검증이 사람 없이 같은 길을 밟는다.
    /// </summary>
    public interface IGameIntent
    {
    }

    /// <summary>
    /// 의도를 받는 자리 — 코어 쪽 입구.
    ///
    /// ★ 받는 쪽이 <b>거절할 수 있다</b> — 값이 모자라거나 상한이면 아무 일도 안 일어난다.
    ///   그 판정은 코어의 몫이지 버튼의 몫이 아니다. 버튼을 비활성화하는 건 친절이지 규칙이 아니다.
    /// </summary>
    public interface IIntentSink<in TIntent> where TIntent : IGameIntent
    {
        /// <summary>의도를 보낸다. 받아들여졌으면 true.</summary>
        bool Send(TIntent intent);
    }
}
