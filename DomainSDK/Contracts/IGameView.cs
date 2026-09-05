namespace WitchMendokusai.DomainSDK.Contracts
{
    /// <summary>
    /// 지금 상태의 <b>읽기 전용 사진</b> — 코어가 표현에게 건네는 것. 마커 인터페이스다.
    ///
    /// ★ 왜 상태를 그대로 안 넘기나 — 넘기는 순간 표현이 코어를 <b>고칠 수 있게</b> 된다.
    ///   그러면 「코어만으로 게임이 돈다」가 거짓이 되고, 표현을 갈아끼울 때마다 판정이 달라진다.
    ///   사진은 사진일 뿐이라 누가 봐도 게임이 안 바뀐다.
    /// </summary>
    public interface IGameSnapshot
    {
    }

    /// <summary>
    /// 코어를 <b>그리는</b> 자리. 3D · 2D · UI · 글자 무엇이든 이 문 하나로 꽂힌다.
    ///
    /// ★ 표현은 코어를 못 바꾼다 — 사진을 받아 그릴 뿐이다. 바꾸고 싶으면 <see cref="IIntentSink{TIntent}"/> 로
    ///   <b>의도를 보낸다</b>(누른다·고른다). 방향이 둘로 갈려 있어야 표현을 통째로 갈아도 게임이 그대로 산다.
    ///
    /// ★ 이 계약이 Unity 를 모르는 이유 — 같은 게임을 헤드리스로 돌려 자동 검증하려면
    ///   글자 표현 하나만 꽂으면 되게 해야 한다. 계약이 엔진을 알면 그게 막힌다.
    /// </summary>
    public interface IGameView<in TSnapshot> where TSnapshot : IGameSnapshot
    {
        /// <summary>이 표현이 어떤 몸인가.</summary>
        PresentationKind Kind { get; }

        /// <summary>이 사진대로 그린다. 매 프레임 불릴 수 있으니 싸야 한다.</summary>
        void Render(TSnapshot snapshot);
    }
}
