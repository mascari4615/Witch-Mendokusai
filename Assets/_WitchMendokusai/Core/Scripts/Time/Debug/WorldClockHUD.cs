using UnityEngine;

namespace WitchMendokusai
{
	// WorldClock 디버그 HUD (IMGUI). 시각 표시 + 속도 슬라이더 (런타임 tweak) + Skip 버튼.
	// WorldClock.OnHourChanged 첫 사용처 — 데드 인터페이스 방지. (TASK-WM-054-A)
	public class WorldClockHUD : MonoBehaviour
	{
		[SerializeField] private bool show = true;
		[SerializeField] private Vector2 anchor = new Vector2(10, 10);

		private string lastHourEvent = "(none)";
		private GUIStyle labelStyle;

		private void OnEnable()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;

			worldClock.OnHourChanged += HandleHourChanged;
		}

		private void OnDisable()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;

			worldClock.OnHourChanged -= HandleHourChanged;
		}

		private void HandleHourChanged(int hour) => lastHourEvent = $"hour → {hour}";

		private void OnGUI()
		{
			if (show == false)
				return;

			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;

			if (worldClock.Config == null)
				return;

			EnsureStyles();

			float width = 340f;
			float height = 130f;
			Rect rect = new Rect(anchor.x, anchor.y, width, height);
			GUI.Box(rect, GUIContent.none);

			GUILayout.BeginArea(new Rect(anchor.x + 10, anchor.y + 8, width - 20, height - 16));

			GUILayout.Label($"<b>WorldClock</b>  {worldClock.ToDebugString()}", labelStyle);
			GUILayout.Label($"paused: {worldClock.IsClockPaused}  /  last: {lastHourEvent}", labelStyle);

			GUILayout.BeginHorizontal();
			GUILayout.Label($"speed: {worldClock.Config.MinutesPerRealSecond:F1} min/sec", labelStyle, GUILayout.Width(170));
			float newSpeed = GUILayout.HorizontalSlider(worldClock.Config.MinutesPerRealSecond, 0.1f, 600f);
			if (Mathf.Approximately(newSpeed, worldClock.Config.MinutesPerRealSecond) == false)
				worldClock.Config.MinutesPerRealSecond = newSpeed;
			GUILayout.EndHorizontal();

			if (GUILayout.Button("Skip to next day"))
				worldClock.SkipToNextDay();

			GUILayout.EndArea();
		}

		private void EnsureStyles()
		{
			if (labelStyle != null)
				return;

			labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
		}
	}
}
