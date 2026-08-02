using System;

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
	///
	/// Tooltip = lazy factory — TooltipController 가 UIRoot 의존이라
	/// TooltipController eager build 도중 UIRoot prefab 이 spawn 되며 OnEnable 이
	/// 발화. OnEnable 에서 즉시 container.Resolve&lt;TooltipController&gt;() 호출 시
	/// 같은 Lazy 재진입 → InvalidOperationException. factory lambda 로 첫 사용
	/// 시점까지 미뤄 cycle break.
	/// </summary>
	public sealed class UIServices : IUIWindowServices
	{
		private readonly Func<TooltipController> tooltipFactory;
		private TooltipController tooltipCache;

		public CodexPreviewController CodexPreview { get; }
		public WindowManager WindowManager { get; }
		public TooltipController Tooltip => tooltipCache ??= tooltipFactory();

		public UIServices(CodexPreviewController codexPreview, WindowManager windowManager, Func<TooltipController> tooltipFactory)
		{
			CodexPreview = codexPreview;
			WindowManager = windowManager;
			this.tooltipFactory = tooltipFactory;
		}
	}
}
