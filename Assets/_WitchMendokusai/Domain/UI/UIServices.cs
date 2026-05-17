namespace WitchMendokusai
{
	/// <summary>
	/// Panel-scoped UI 서비스 핸들. DI 거주 UIRoot 가 panel-root VisualElement 의
	/// userData 에 1회 owner-push (WM-115 ②owner-push). UXML-cloned VisualElement
	/// 가 static Instance reach 대신 panel context 로 획득 — global Singleton 결합
	/// 제거 (TASK-WM-133).
	///
	/// Core 의 WMWindow 는 Domain→Core 단방향이라 본 타입 직접 참조 불가 →
	/// Core-정의 IUIWindowServices facet 으로만 WindowManager 획득(DIP). 증분 3
	/// 에서 TooltipController(Domain) 직접 프로퍼티 추가.
	/// </summary>
	public sealed class UIServices : IUIWindowServices
	{
		public CodexPreviewController CodexPreview { get; }
		public WindowManager WindowManager { get; }
		public TooltipController Tooltip { get; }

		public UIServices(CodexPreviewController codexPreview, WindowManager windowManager, TooltipController tooltip)
		{
			CodexPreview = codexPreview;
			WindowManager = windowManager;
			Tooltip = tooltip;
		}
	}
}
