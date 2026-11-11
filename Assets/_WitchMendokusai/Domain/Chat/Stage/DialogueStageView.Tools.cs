using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 입력과 자동, 건너뛰기, 로그 부분
	public partial class DialogueStageView
	{
		private const string USS_TOOL_ON = "wm-dlg-tool--on";
		private const string USS_LOG_OPEN = "wm-dlg-log--open";
		private const string USS_LOG_ENTRY = "wm-dlg-log__entry";
		private const string USS_LOG_CHOICE = "wm-dlg-log__entry--choice";

		private VisualElement tools;
		private Button autoButton;
		private VisualElement logPanel;
		private ScrollView logScroll;
		private bool isLogOpen;

		private void BuildTools()
		{
			tools = root.Q<VisualElement>("wm-dlg-tools");
			autoButton = root.Q<Button>("wm-dlg-tool-auto");
			autoButton.clicked += ToggleAuto;
			root.Q<Button>("wm-dlg-tool-skip").clicked += SkipLines;
			root.Q<Button>("wm-dlg-tool-log").clicked += ToggleLog;
			logPanel = root.Q<VisualElement>("wm-dlg-log");
			logPanel.style.display = DisplayStyle.None;
			logScroll = root.Q<ScrollView>("wm-dlg-log-scroll");
			root.Q<Button>("wm-dlg-log-close").clicked += CloseLogWithSound;
		}

		private void TickInput(float deltaTime)
		{
			if (presentation == DialogueStagePresentation.World && isChoosing == false)
				return;
			if (Time.frameCount <= ignoreInputUntilFrame)
				return;
			if (UIToolkitFocus.IsAnyTextFieldFocused() || uiRoot.SettingView.IsOpen)
				return;

			if (inputManager.WasActionPressedThisFrame(InputEventType.DialogueLog))
			{
				ToggleLog();
				return;
			}
			if (isLogOpen)
				return;

			if (inputManager.WasActionPressedThisFrame(InputEventType.DialogueAuto))
			{
				ToggleAuto();
				return;
			}
			if (inputManager.WasActionPressedThisFrame(InputEventType.DialogueSkip))
			{
				SkipLines();
				return;
			}

			if (isChoosing)
			{
				if (inputManager.WasActionPressedThisFrame(InputEventType.Submit))
					SubmitFocusedChoice();
				return;
			}

			bool wasAdvancePressed = inputManager.WasActionPressedThisFrame(InputEventType.Submit)
				|| (inputManager.IsAnyKeyboardKeyPressedThisFrame && inputManager.WasActionPressedThisFrame(InputEventType.Cancel) == false);
			if (wasAdvancePressed)
				PressAdvance();

			if (advancer.Tick(deltaTime))
				AdvanceToNextLine();
			RenderLine();
		}

		private void OnRootPointerDown(PointerDownEvent evt)
		{
			if (isChoosing || isLogOpen || isSessionOpen == false)
				return;
			if (evt.target is VisualElement target && tools.Contains(target))
				return;
			PressAdvance();
		}

		private void PressAdvance()
		{
			if (advancer.Press())
				AdvanceToNextLine();
			RenderLine();
		}

		private void AdvanceToNextLine()
		{
			if (controls == null || controls.CanAdvanceLine == false)
				return;
			UiSfx.Play(UiSfxEvent.DialogueAdvance);
			controls.AdvanceLine();
		}

		private void ToggleAuto()
		{
			advancer.AutoEnabled = advancer.AutoEnabled == false;
			RefreshAutoButton();
			UiSfx.Play(UiSfxEvent.Confirm);
		}

		private void RefreshAutoButton() => autoButton.EnableInClassList(USS_TOOL_ON, advancer.AutoEnabled);

		private void SkipLines()
		{
			if (controls == null || isChoosing)
				return;

			UiSfx.Play(UiSfxEvent.Confirm);
			isSkipping = true;
			controls.Skip();
			isSkipping = false;
			RenderLine();
		}

		private void ToggleLog()
		{
			if (isLogOpen)
			{
				CloseLogWithSound();
				return;
			}
			isLogOpen = true;
			UiSfx.Play(UiSfxEvent.Confirm);
			RefreshLog();
			logPanel.AddToClassList(USS_LOG_OPEN);
			logPanel.style.display = DisplayStyle.Flex;
		}

		private void RefreshLog()
		{
			logScroll.Clear();
			if (controls == null)
				return;

			foreach (DialogueTranscript.Entry entry in controls.Transcript.Entries)
			{
				string speakerText = entry.IsChoice ? "선택" : entry.Speaker;
				Label label = new(string.IsNullOrEmpty(speakerText) ? entry.Text : $"{speakerText}: {entry.Text}");
				label.AddToClassList(USS_LOG_ENTRY);
				label.EnableInClassList(USS_LOG_CHOICE, entry.IsChoice);
				logScroll.Add(label);
			}
			logScroll.schedule.Execute(() => logScroll.scrollOffset = new Vector2(0f, float.MaxValue));
		}

		private void CloseLogWithSound()
		{
			UiSfx.Play(UiSfxEvent.Cancel);
			CloseLog();
		}

		private void CloseLog()
		{
			isLogOpen = false;
			logPanel.RemoveFromClassList(USS_LOG_OPEN);
			logPanel.style.display = DisplayStyle.None;
		}

		// 취소 입력 우선권. 로그가 열려 있으면 로그만 닫음
		public bool TryHandleCancel()
		{
			if (isLogOpen == false)
				return false;
			CloseLogWithSound();
			return true;
		}
	}
}
