using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	public sealed class ShopPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly float feedbackSeconds;
		private readonly Button pullButton;
		private readonly Label pullOdds;
		private readonly Button bagButton;
		private readonly Label bagNote;

		public ShopPageController(VisualElement page, IdleSession session, UIContentSO content,
			Action writeDown, Action requestRender, Action<string, float> showFeedback, float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.feedbackSeconds = feedbackSeconds;
			pullButton = page.Q<Button>("pull-button");
			pullButton.clicked += Pull;
			pullOdds = page.Q<Label>("pull-odds");
			bagButton = page.Q<Button>("bag-button");
			bagButton.clicked += BuyBag;
			bagNote = page.Q<Label>("bag-note");
		}

		public void Render(IdleSnapshot snapshot)
		{
			bagButton.text = snapshot.BagUpgradeCost > 0d
				? content.BagUpgradeText(IdleShop.BAG_STEP_HINT, BigNumberText.Format(snapshot.BagUpgradeCost))
				: content.BagUpgradeMaxText;
			bagButton.SetEnabled(snapshot.CanBuyBag);
			bagNote.text = content.BagResetNoteText(snapshot.BagCapacity);
			pullButton.text = snapshot.CanPull
				? content.PullAvailableText(BigNumberText.Format(snapshot.PullCost), snapshot.PullStoneCost, snapshot.Stones)
				: snapshot.Stones < snapshot.PullStoneCost
					? content.PullNoStoneText(snapshot.Stones)
					: content.PullNoGoldText(BigNumberText.Format(snapshot.PullCost));
			pullButton.SetEnabled(snapshot.CanPull);
			pullOdds.text = content.PullOddsText(
				snapshot.LegendChance, snapshot.EpicChance, snapshot.RareChance, snapshot.PullsToPity);
		}

		private void BuyBag()
		{
			session.BuyBagUpgrade();
			requestRender();
		}

		private void Pull()
		{
			if (session.TryPull(out IdleHeroPull result) == false)
			{
				return;
			}

			IdleHeroKind kind = IdleHeroes.KindOf(result.Id);
			showFeedback(content.PullFeedbackText(
				content.GradeName(result.Grade), kind.Name, result.IsNew, result.ByPity), feedbackSeconds * 2f);
			writeDown();
			requestRender();
		}
	}
}
