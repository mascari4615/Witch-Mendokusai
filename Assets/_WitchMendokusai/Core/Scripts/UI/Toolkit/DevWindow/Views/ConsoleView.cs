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

		private const float AUTO_SCROLL_THRESHOLD = 4f;

		private readonly ScrollView scrollView;

		public ConsoleView()
		{
			AddToClassList(USS_CLASS);

			scrollView = new ScrollView(ScrollViewMode.Vertical);
			scrollView.style.flexGrow = 1;
			Add(scrollView);
		}

		public void AppendLog(string message, LogLevel level)
		{
			// 추가 전 sticky 판정 — 새 line 추가로 highValue 가 늘어나면 value < highValue 가 돼
			// "위로 스크롤한 것"처럼 보이는 오판 방지.
			bool wasSticky = IsAtBottom();

			Label line = new(message);
			line.AddToClassList(USS_LINE);
			line.AddToClassList(LevelClass(level));
			scrollView.Add(line);

			if (wasSticky == false)
				return;

			// ScrollView.ScrollTo(line) 은 layout 끝난 뒤 element 가 보이도록 스크롤. scrollOffset
			// 직접 설정은 highValue 가 갱신되기 전에 clamp 돼 안 먹는 경우가 있음 — ScrollTo 가 안전.
			line.RegisterCallback<GeometryChangedEvent>(evt =>
			{
				if (evt.target is Label target)
					scrollView.ScrollTo(target);
			});
		}

		public void Clear()
		{
			scrollView.Clear();
		}

		private bool IsAtBottom()
		{
			Scroller verticalScroller = scrollView.verticalScroller;
			if (verticalScroller == null)
				return true;

			float distanceFromBottom = verticalScroller.highValue - verticalScroller.value;
			return distanceFromBottom <= AUTO_SCROLL_THRESHOLD;
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
