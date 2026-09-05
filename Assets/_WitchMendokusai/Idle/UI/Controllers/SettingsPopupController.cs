using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class SettingsPopupController
	{
		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly Action requestRender;
		private readonly List<Button> speedButtons = new List<Button>();
		private readonly Label logLabel;
		private readonly Label noteLabel;
		private float noteSecondsLeft;

		public SettingsPopupController(VisualElement popup, ModalController modalController,
			IdleSession session, UIContentSO content, Action requestRender, Action wipeAndRestart)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.session = session;
			this.content = content;
			this.requestRender = requestRender;
			modalController.Register(popup, Close);
			popup.Q<Button>("settings-close").clicked += Close;
			for (int index = 0; ; index++)
			{
				Button button = popup.Q<Button>("speed-" + index);
				if (button == null)
				{
					break;
				}

				int captured = index;
				button.clicked += () => SetSpeed(captured);
				speedButtons.Add(button);
			}

			// 데이터 초기화는 설정 안으로 (사용자 2026-09-05). 전투 화면에 늘 떠 있을 것이 아님
			Button wipe = popup.Q<Button>("wipe-button");
			wipe.style.display = UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild
				? DisplayStyle.Flex
				: DisplayStyle.None;
			wipe.clicked += wipeAndRestart;

			logLabel = popup.Q<Label>("log-label");
			noteLabel = popup.Q<Label>("note-label");
		}

		public void Open(Action beforeOpen)
		{
			beforeOpen();
			modalController.Show(popup);
		}

		public void Close()
		{
			modalController.Hide(popup);
		}

		public void Render(IdleSnapshot snapshot)
		{
			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);
			logLabel.text = content.AdviceText(
				advice.Step, advice.Amount, content.DescribeSpan(advice.Amount));
			for (int index = 0; index < speedButtons.Count; index++)
			{
				speedButtons[index].EnableInClassList("idle-settings-speed--selected",
					Math.Abs(snapshot.Speed - (index + 1d)) < 0.001d);
			}
		}

		public void Tick(float delta)
		{
			if (noteSecondsLeft <= 0f)
			{
				return;
			}

			noteSecondsLeft -= delta;
			noteLabel.style.opacity = noteSecondsLeft < 1f ? noteSecondsLeft : 1f;
			if (noteSecondsLeft <= 0f)
			{
				noteLabel.text = string.Empty;
			}
		}

		public void ShowNote(string text, float seconds)
		{
			noteLabel.text = text;
			noteLabel.style.opacity = 1f;
			noteSecondsLeft = seconds;
		}

		private void SetSpeed(int step)
		{
			session.SetSpeedStep(step);
			requestRender();
		}
	}
}
