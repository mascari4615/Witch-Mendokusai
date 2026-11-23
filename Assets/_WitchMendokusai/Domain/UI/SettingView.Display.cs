using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 화면 탭: 전체화면, 해상도, 수직 동기, FPS 제한, FPS 표시. 값과 적용은 DisplaySettings
	public partial class SettingView
	{
		private const string TAB_KEY_DISPLAY = "display";
		private const string STEP_SLIDER_CLASS = "wm-setting-slider--step";
		private const string WIDE_VALUE_CLASS = "wm-setting-value--wide";
		private const string ROW_DISABLED_CLASS = "wm-setting-row--disabled";

		[SerializeField, Tooltip("FPS 제한 선택지. 0 모니터 맞춤, -1 제한 없음")] private int[] frameRateChoices = { 30, 60, 120, 144, DisplaySettings.FRAME_RATE_MONITOR, DisplaySettings.FRAME_RATE_UNLIMITED };
		[SerializeField, Tooltip("해상도 목록에 넣을 가장 낮은 세로 크기")] private int minResolutionHeight = 720;
		[SerializeField, Tooltip("해상도 막대를 멈추고 이만큼 지나야 적용. 끄는 동안 창 크기가 여러 번 바뀌지 않게")] private long resolutionApplyDelayMs = 400;

		private Toggle fullscreenToggle;
		private Slider resolutionSlider;
		private Label resolutionValue;
		private List<Vector2Int> resolutionChoices;
		private IVisualElementScheduledItem pendingResolutionApply;
		private Slider frameRateSlider;
		private Label frameRateValue;

		private VisualElement BuildDisplayContent()
		{
			VisualElement content = new VisualElement();

			Label screenHeader = new Label("화면");
			screenHeader.AddToClassList("wm-setting-header");
			content.Add(screenHeader);

			fullscreenToggle = CreateToggle("전체화면", DisplaySettings.Fullscreen, DescribeFullscreen, isOn => DisplaySettings.Fullscreen = isOn, content);
			content.Add(CreateResolutionSlider());

			Label frameHeader = new Label("프레임");
			frameHeader.AddToClassList("wm-setting-header");
			content.Add(frameHeader);

			CreateToggle("수직 동기", DisplaySettings.VSync, DescribeOnOff, OnVSyncChanged, content);
			content.Add(CreateFrameRateSlider());
			fpsDisplayToggle = CreateToggle("FPS 표시", UIFPS.IsVisible, DescribeOnOff, isOn => UIFPS.IsVisible = isOn, content);

			RefreshFrameRateRow();
			return content;
		}

		private static string DescribeFullscreen(bool isOn) => isOn ? "켜짐" : "꺼짐 (창 모드)";

		private VisualElement CreateResolutionSlider()
		{
			resolutionChoices = CollectResolutions(Screen.resolutions, DisplaySettings.Resolution, minResolutionHeight);
			int current = resolutionChoices.IndexOf(DisplaySettings.Resolution);
			VisualElement row = CreateStepSlider("해상도", resolutionChoices.Count, current, index => FormatResolution(resolutionChoices[index]),
				ScheduleResolutionApply, out resolutionSlider, out resolutionValue);
			// 고를 것이 하나뿐이면 (원격 화면, 이상한 드라이버) 막대를 잠금
			if (resolutionChoices.Count < 2)
				resolutionSlider.SetEnabled(false);
			return row;
		}

		private static string FormatResolution(Vector2Int resolution) => $"{resolution.x} x {resolution.y}";

		// 끄는 동안이나 키를 연달아 누르는 동안은 글자만 바꾸고, 멈춘 뒤 한 번 적용
		private void ScheduleResolutionApply(int index)
		{
			pendingResolutionApply?.Pause();
			Vector2Int chosen = resolutionChoices[index];
			pendingResolutionApply = resolutionSlider.schedule.Execute(() => DisplaySettings.Resolution = chosen).StartingIn(resolutionApplyDelayMs);
		}

		// 창을 연 사이 Alt+Enter 로 모드가 바뀌었을 수 있음. 실제 화면 상태를 저장값과 줄에 맞춤
		private void SyncDisplayRows()
		{
			bool isFullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
			if (Application.isEditor == false && isFullscreen != DisplaySettings.Fullscreen)
				DisplaySettings.StoreFullscreenWithoutApply(isFullscreen);
			SyncToggle(fullscreenToggle, DisplaySettings.Fullscreen, DescribeFullscreen);

			int index = resolutionChoices.IndexOf(DisplaySettings.Resolution);
			if (index >= 0)
			{
				resolutionSlider.SetValueWithoutNotify(index);
				resolutionValue.text = FormatResolution(resolutionChoices[index]);
			}
		}

		private VisualElement CreateFrameRateSlider()
		{
			int current = Array.IndexOf(frameRateChoices, DisplaySettings.FrameRateLimit);
			// 지금 값이 선택지에 없으면 (선택지를 바꾼 뒤의 옛 값) 모니터 맞춤 칸
			if (current < 0)
				current = Array.IndexOf(frameRateChoices, DisplaySettings.FRAME_RATE_MONITOR);
			VisualElement row = CreateStepSlider("FPS 제한", frameRateChoices.Length, current, index => FormatFrameRate(frameRateChoices[index]),
				index => DisplaySettings.FrameRateLimit = frameRateChoices[index], out frameRateSlider, out frameRateValue);
			return row;
		}

		private void OnVSyncChanged(bool isOn)
		{
			DisplaySettings.VSync = isOn;
			RefreshFrameRateRow();
		}

		// 수직 동기가 켜지면 FPS 제한은 Unity 가 무시. 줄을 흐리게 하고 이유를 값 자리에
		private void RefreshFrameRateRow()
		{
			bool isLocked = DisplaySettings.VSync;
			frameRateSlider.parent.EnableInClassList(ROW_DISABLED_CLASS, isLocked);
			frameRateSlider.SetEnabled(isLocked == false);
			int index = Mathf.Clamp(Mathf.RoundToInt(frameRateSlider.value), 0, frameRateChoices.Length - 1);
			frameRateValue.text = isLocked ? "수직 동기 사용 중" : FormatFrameRate(frameRateChoices[index]);
		}

		private static string FormatFrameRate(int frameRate)
		{
			if (frameRate == DisplaySettings.FRAME_RATE_MONITOR)
				return "모니터 맞춤";
			return frameRate == DisplaySettings.FRAME_RATE_UNLIMITED ? "제한 없음" : frameRate.ToString();
		}

		/// <summary> 모니터가 지원하는 해상도에서 같은 크기를 하나로, 세로가 너무 작은 것은 뺌. 지금 값은 목록에 없어도 넣음 </summary>
		public static List<Vector2Int> CollectResolutions(Resolution[] supported, Vector2Int current, int minHeight)
		{
			List<Vector2Int> result = new();
			foreach (Resolution resolution in supported)
			{
				Vector2Int size = new Vector2Int(resolution.width, resolution.height);
				if (size.y < minHeight || result.Contains(size))
					continue;
				result.Add(size);
			}
			if (result.Contains(current) == false)
				result.Add(current);
			result.Sort((left, right) => left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y));
			return result;
		}

		// 칸 단위로 고르는 막대 줄. 키보드 좌우는 한 칸씩 (Navigation 의 STEP_SLIDER_CLASS)
		private VisualElement CreateStepSlider(string label, int count, int index, Func<int, string> format, Action<int> onChosen, out Slider slider, out Label valueLabel)
		{
			int last = count - 1;
			// 칸이 하나면 0 에서 0 까지의 막대는 손잡이 자리를 못 정함. 범위만 한 칸 넓히고 값은 아래에서 막음
			Slider stepSlider = CreateSlider(label, 0f, Mathf.Max(last, 1), index);
			stepSlider.AddToClassList(STEP_SLIDER_CLASS);
			VisualElement row = new VisualElement();
			row.AddToClassList("wm-setting-row");
			row.Add(stepSlider);
			Label value = new Label(format(index));
			value.AddToClassList("wm-setting-value");
			value.AddToClassList(WIDE_VALUE_CLASS);
			row.Add(value);
			stepSlider.RegisterValueChangedCallback(changeEvent =>
			{
				int next = Mathf.Clamp(Mathf.RoundToInt(changeEvent.newValue), 0, last);
				stepSlider.SetValueWithoutNotify(next);
				if (next == Mathf.RoundToInt(changeEvent.previousValue))
					return;
				value.text = format(next);
				onChosen(next);
				UiSfx.Play(UiSfxEvent.Slider);
			});
			slider = stepSlider;
			valueLabel = value;
			return row;
		}
	}
}
