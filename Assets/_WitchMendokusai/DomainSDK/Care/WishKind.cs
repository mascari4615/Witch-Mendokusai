namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 「되살림, 그리고 배웅」 — 소원의 구조적 범주(데이터 분류용 enum, 디자인 영역과 직교).
    ///
    /// 한 캐릭터의 미완의 소원이 어떤 *모양* 인지 — 재료를 모아주는 일인지, 함께 있어주는 일인지,
    /// 옛 관계를 화해하는 일인지, 마지막을 정리하는 일인지. 본 enum 은 *카테고리* 만 박는다 —
    /// 어떤 캐릭터가 어느 카테고리를 갖는지는 데이터(WishSpec 인스턴스)가 결정. 코드는 카테고리에
    /// 의미를 두지 않고 균등 취급(미래 UI 가 아이콘·문구로 톤 분리).
    ///
    /// ⚠ DEFERRED — 본 enum 은 콘텐츠 구조용 골격. WM-168 Tomodachi 자율 삶 레이어 통합 여부
    /// (별도 콘텐츠 vs 결말 레이어 흡수) 와 무관하게 의미가 유지되도록 카테고리만 분리.
    /// </summary>
    public enum WishKind
    {
        /// <summary>재료·물건을 모아주는 소원 — 마도서 페이지 = 누군가가 그리워하는 무언가를 만들어주기.</summary>
        Material = 0,

        /// <summary>함께 있어주는 소원 — 외로움·고립·기다림의 해소. 마음 곁의 시간이 본체.</summary>
        Companionship = 1,

        /// <summary>옛 관계를 화해·정리하는 소원 — 한(恨)·후회·미안함의 마무리.</summary>
        Reconciliation = 2,

        /// <summary>마지막을 정리하는 소원 — 떠나기 전 마지막 인사·유품·약속.</summary>
        Closure = 3,
    }
}
