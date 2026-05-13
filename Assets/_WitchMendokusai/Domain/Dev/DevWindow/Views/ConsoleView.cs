using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 명령 결과 누적 출력. ScrollView + Label per line. 색상 4종.
	/// 사용자가 위로 스크롤한 상태면 자동 스크롤 OFF (로그 읽는 중 끊기지 않게).
	/// Debug.LogError 인터셉트 안 함 — Unity Console 과 분리.
	/// </summary>
	public class ConsoleView : VisualElement
	{
		public enum LogLevel
		{
			Info,
			Success,
			Warn,
			Error,
		}

		public const string USS_CLASS = "wm-dev-console";
		public const string USS_LINE = "wm-dev-console__line";
		public const string USS_LINE_INFO = "wm-dev-log-info";
		public const string USS_LINE_SUCCESS = "wm-dev-log-success";
		public const string USS_LINE_WARN = "wm-dev-log-warn";
		public const string USS_LINE_ERROR = "wm-dev-log-error";

		private readonly ScrollView scrollView;
		private bool pendingScrollToBottom;

		public ConsoleView()
		{
			AddToClassList(USS_CLASS);

			scrollView = new ScrollView(ScrollViewMode.Vertical);
			scrollView.style.flexGrow = 1;
			Add(scrollView);

			// content 의 layout 변할 때 (새 line 추가 후) 강제로 최하단.
			scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);
		}

		public void AppendLog(string message, LogLevel level)
		{
			Label line = new(message);
			line.AddToClassList(USS_LINE);
			line.AddToClassList(LevelClass(level));
			scrollView.Add(line);

			// 항상 auto-scroll — sticky 판정 없음. 사용자가 위로 스크롤해도 새 로그 오면 끝까지 내려감.
			pendingScrollToBottom = true;
		}

		private void OnContentGeometryChanged(GeometryChangedEvent evt)
		{
			if (pendingScrollToBottom == false)
				return;

			pendingScrollToBottom = false;
			ScrollToBottom();
		}

		private void ScrollToBottom()
		{
			Scroller scroller = scrollView.verticalScroller;
			if (scroller == null)
			{
				scrollView.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
				return;
			}

			if (scroller.highValue <= scroller.lowValue)
				return;

			scroller.value = scroller.highValue;
		}

		public new void Clear()
		{
			scrollView.Clear();
		}

		private static string LevelClass(LogLevel level) => level switch
		{
			LogLevel.Info => USS_LINE_INFO,
			LogLevel.Success => USS_LINE_SUCCESS,
			LogLevel.Warn => USS_LINE_WARN,
			LogLevel.Error => USS_LINE_ERROR,
			_ => USS_LINE_INFO,
		};
	}
}
