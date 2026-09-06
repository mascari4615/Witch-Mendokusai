using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class LabPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly float feedbackSeconds;
		private readonly Label summary;
		private readonly Button button;

		public LabPageController(VisualElement page, IdleSession session, UIContentSO content,
			Action writeDown, Action requestRender, Action<string, float> showFeedback, float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.feedbackSeconds = feedbackSeconds;
			summary = page.RequireQ<Label>("prestige-summary");
			button = page.RequireQ<Button>("prestige-button");
			button.clicked += Prestige;
		}

		public void Render(IdleSnapshot snapshot)
		{
			summary.text = content.PrestigeSummaryText(
				snapshot.PrestigePoints, snapshot.PrestigeAward, snapshot.PrestigeMultiplier);
			button.text = content.PrestigeButtonText(snapshot.PrestigeAward, snapshot.PrestigeNextStage);
			button.SetEnabled(snapshot.PrestigeAward > 0L);
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				showFeedback(content.PrestigeFeedback, feedbackSeconds * 2f);
				writeDown();
			}

			requestRender();
		}
	}
}
