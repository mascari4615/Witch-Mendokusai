namespace WitchMendokusai
{
	/// <summary>
	/// Panel-scoped UI 서비스 핸들. DI 거주 UIRoot 가 panel-root VisualElement 의
	/// userData 에 1회 owner-push (WM-115 ②owner-push). UXML-cloned VisualElement
	/// 가 static Instance reach 대신 panel context 로 획득 — global Singleton 결합
	/// 제거 (TASK-WM-133). 증분 2/3 에서 WindowManager/TooltipController 추가.
	/// </summary>
	public sealed class UIServices
	{
		public CodexPreviewController CodexPreview { get; }

		public UIServices(CodexPreviewController codexPreview)
		{
			CodexPreview = codexPreview;
		}
	}
}
