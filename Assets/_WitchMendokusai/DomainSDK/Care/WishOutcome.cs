namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 — 소원이 완성됐을 때 그 캐릭터가 맞이하는 결말 분기.
    ///
    /// ⚠ LORE-DEFERRED — WM 정본 "인형 영구 거주(떠남 없음)" 와 본 콘텐츠의 '배웅' 사이 긴장은
    /// 디자인 영역 결정사항. 본 enum 은 *양쪽 갈래를 모두 모델링* 해 둠 — 인형은 데이터(WishSpec)
    /// 에서 항상 <see cref="Settle"/> 만 갖도록 두고, 외부 존재(마을 사람/영혼/마족)는 캐릭터별로
    /// <see cref="Depart"/> / <see cref="Settle"/> 데이터로 결정. 코드는 어느 쪽이 옳다고 판단 X.
    ///
    /// 톤 결정(Frieren식 잔잔한 멜랑콜리 vs 따뜻한 졸업) 도 데이터·UI 영역 — 본 모델은 분기만 노출.
    /// </summary>
    public enum WishOutcome
    {
        /// <summary>평온히 떠난다 — 작별. 메모리얼에 흔적이 남는 결말 갈래.</summary>
        Depart = 0,

        /// <summary>마을에 자리잡는다 — '진짜가 되어 머문다' 식 정착. 떠나지 않는 결말 갈래.</summary>
        Settle = 1,
    }
}
