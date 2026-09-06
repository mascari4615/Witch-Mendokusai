using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 상점 페이지 (layout.md 표 3, 구현 순서 10). 배너 (픽업 초상, 천장 카운터), 1회와 묶음 뽑기, 확률표, 무료 상자, 가방
	///
	/// ★ 규칙은 한 줄도 없음. 값, 보장, 픽업, 날짜 판정은 전부 사진과 세션
	/// </summary>
	public sealed class ShopPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly HeroVisualPresenter heroVisualPresenter;
		private readonly Action openOdds;
		private readonly Action<IReadOnlyList<IdleHeroPull>> showGacha;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly float feedbackSeconds;
		private readonly VisualElement pickupPortrait;
		private readonly Label pickupLabel;
		private readonly Label pityLabel;
		private readonly Button pullButton;
		private readonly Button pullBatchButton;
		private readonly Label pullOdds;
		private readonly Button oddsButton;
		private readonly Button freeBoxButton;
		private readonly Button bagButton;
		private readonly Label bagNote;
		private readonly List<IdleHeroPull> batchResult = new List<IdleHeroPull>();

		public ShopPageController(VisualElement page, IdleSession session, UIContentSO content,
			HeroVisualPresenter heroVisualPresenter, Action openOdds,
			Action<IReadOnlyList<IdleHeroPull>> showGacha,
			Action writeDown, Action requestRender, Action<string, float> showFeedback, float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.heroVisualPresenter = heroVisualPresenter;
			this.openOdds = openOdds;
			this.showGacha = showGacha;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.feedbackSeconds = feedbackSeconds;
			pickupPortrait = page.RequireQ<VisualElement>("pickup-portrait");
			pickupLabel = page.RequireQ<Label>("pickup-label");
			pityLabel = page.RequireQ<Label>("pity-label");
			pullButton = page.RequireQ<Button>("pull-button");
			pullButton.clicked += Pull;
			pullBatchButton = page.RequireQ<Button>("pull-batch-button");
			pullBatchButton.clicked += PullBatch;
			pullOdds = page.RequireQ<Label>("pull-odds");
			oddsButton = page.RequireQ<Button>("odds-button");
			oddsButton.clicked += () => this.openOdds();
			oddsButton.text = content.OddsButtonText;
			freeBoxButton = page.RequireQ<Button>("free-box-button");
			freeBoxButton.clicked += OpenFreeBox;
			bagButton = page.RequireQ<Button>("bag-button");
			bagButton.clicked += BuyBag;
			bagNote = page.RequireQ<Label>("bag-note");
		}

		public void Render(IdleSnapshot snapshot)
		{
			RenderBanner(snapshot);

			pullButton.text = snapshot.CanPull
				? content.PullAvailableText(BigNumberText.Format(snapshot.PullCost), snapshot.PullStoneCost, snapshot.Stones)
				: snapshot.Stones < snapshot.PullStoneCost
					? content.PullNoStoneText(snapshot.Stones)
					: content.PullNoGoldText(BigNumberText.Format(snapshot.PullCost));
			pullButton.SetEnabled(snapshot.CanPull);

			pullBatchButton.text = content.PullBatchText(
				snapshot.PullBatchCount,
				BigNumberText.Format(snapshot.PullBatchCost),
				snapshot.PullBatchStoneCost,
				content.GradeName(snapshot.PullBatchFloorGrade));
			pullBatchButton.SetEnabled(snapshot.CanPullBatch);

			pullOdds.text = content.PullOddsText(
				snapshot.LegendChance, snapshot.EpicChance, snapshot.RareChance, snapshot.PullsToPity);

			freeBoxButton.text = snapshot.FreeBoxReady
				? content.FreeBoxReadyText(snapshot.FreeBoxStones)
				: content.FreeBoxWaitText(content.DescribeSpan(snapshot.FreeBoxSecondsLeft));
			freeBoxButton.SetEnabled(snapshot.FreeBoxReady);

			bagButton.text = snapshot.BagUpgradeCost > 0d
				? content.BagUpgradeText(IdleShop.BAG_STEP_HINT, BigNumberText.Format(snapshot.BagUpgradeCost))
				: content.BagUpgradeMaxText;
			bagButton.SetEnabled(snapshot.CanBuyBag);
			bagNote.text = content.BagResetNoteText(snapshot.BagCapacity);
		}

		/// <summary>배너. 픽업 초상과 이름, 교체까지 남은 시간, 천장 카운터</summary>
		private void RenderBanner(IdleSnapshot snapshot)
		{
			bool hasPickup = snapshot.PickupHeroId >= 0 && IdleHeroes.Knows(snapshot.PickupHeroId);
			if (hasPickup)
			{
				heroVisualPresenter.SetPortrait(pickupPortrait, snapshot.PickupHeroId);
				pickupLabel.text = content.PickupText(
					IdleHeroes.KindOf(snapshot.PickupHeroId).Name,
					snapshot.PickupWeight,
					content.DescribeSpan(snapshot.PickupSecondsLeft));
			}
			else
			{
				pickupPortrait.style.backgroundImage = StyleKeyword.None;
				pickupLabel.text = content.PickupNoneText;
			}

			pityLabel.text = content.PityCounterText(snapshot.PullsToPity);
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

			// 한 번 뽑아도 연출을 탄다 (사용자 2026-09-05: 버튼 딸깍은 안 됨)
			batchResult.Clear();
			batchResult.Add(result);
			showGacha(batchResult);
			writeDown();
			requestRender();
		}

		private void PullBatch()
		{
			batchResult.Clear();
			if (session.TryPullBatch(batchResult) == false)
			{
				return;
			}

			int legend = 0;
			int epic = 0;
			int rare = 0;
			int newFaces = 0;
			for (int index = 0; index < batchResult.Count; index++)
			{
				IdleHeroPull one = batchResult[index];
				legend += one.Grade == IdleHeroGrade.Legend ? 1 : 0;
				epic += one.Grade == IdleHeroGrade.Epic ? 1 : 0;
				rare += one.Grade == IdleHeroGrade.Rare ? 1 : 0;
				newFaces += one.IsNew ? 1 : 0;
			}

			showGacha(batchResult);
			writeDown();
			requestRender();
		}

		private void OpenFreeBox()
		{
			if (session.TryOpenFreeBox(out long stones) == false)
			{
				return;
			}

			showFeedback(content.FreeBoxFeedbackText(stones), feedbackSeconds);
			writeDown();
			requestRender();
		}
	}
}
