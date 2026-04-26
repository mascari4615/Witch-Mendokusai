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
			Label line = new(message);
			line.AddToClassList(USS_LINE);
			line.AddToClassList(LevelClass(level));
			scrollView.Add(line);

			// 새 line 의 layout 이 계산된 직후 스크롤 — schedule.StartingIn(0)은 layout 전에 실행되어
			// scrollView.highValue 가 아직 갱신되지 않아 clamp 됨. GeometryChangedEvent 로 해결.
			line.RegisterCallback<GeometryChangedEvent>(OnLineGeometryChanged);
		}

		private void OnLineGeometryChanged(GeometryChangedEvent evt)
		{
			if (evt.target is Label line)
				line.UnregisterCallback<GeometryChangedEvent>(OnLineGeometryChanged);

			TryAutoScroll();
		}

		public void Clear()
		{
			scrollView.Clear();
		}

		private void TryAutoScroll()
		{
			Scroller verticalScroller = scrollView.verticalScroller;
			if (verticalScroller == null)
				return;

			float distanceFromBottom = verticalScroller.highValue - verticalScroller.value;

			// 사용자가 위로 스크롤한 상태(거리 큼)면 자동 스크롤 안 함
			if (distanceFromBottom > AUTO_SCROLL_THRESHOLD && verticalScroller.value > 0)
				return;

			scrollView.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
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
