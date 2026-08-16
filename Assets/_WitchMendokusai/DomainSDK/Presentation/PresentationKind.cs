namespace WitchMendokusai.DomainSDK.Presentation
{
    /// <summary>
    /// 같은 코어를 <b>어떤 몸으로</b> 보여주나 (TASK-WM-406).
    ///
    /// ★ 코어만으로도 게임은 돈다 — 대상이 쓰러지고 자원이 쌓이는 판정은 화면 없이 끝난다.
    ///   그걸 3D 로 터뜨릴지, 2D 스프라이트로 굴릴지, 숫자만 올릴지, 글 한 줄로 적을지는
    ///   <b>표현 주체가 정한다.</b> 코어는 자기가 어떻게 보이는지 몰라야 한다.
    ///
    /// ★ 그래서 이 enum 은 코어가 읽는 값이 아니다 — 표현끼리 자기를 밝히는 이름표다.
    ///   (호스트가 「지금 어떤 몸을 붙였나」를 알아야 할 때만 쓴다.)
    /// </summary>
    public enum PresentationKind
    {
        /// <summary>3D 오브젝트로 그린다.</summary>
        Model3D = 0,

        /// <summary>2D 스프라이트로 그린다.</summary>
        Sprite2D = 1,

        /// <summary>화면 요소만으로 그린다 (UI Toolkit 등). 방치·경영물의 기본형.</summary>
        UIOnly = 2,

        /// <summary>글자만으로 그린다. 로그·콘솔·접근성·자동 검증에 쓴다.</summary>
        Text = 3,
    }
}
