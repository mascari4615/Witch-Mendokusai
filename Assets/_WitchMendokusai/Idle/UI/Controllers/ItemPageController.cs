using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>아이템 화면의 가방과 감정 표시와 명령. 공방은 <see cref="ForgePanelController"/></summary>
	public sealed class ItemPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly GearVisualPresenter gearVisualPresenter;
		private readonly VisualTreeAsset bagCellAsset;
		private readonly VisualTreeAsset rowButtonAsset;
		private readonly Func<int> selectedHeroId;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly Action<VisualElement, Func<string>> hookTooltip;
		private readonly float feedbackSeconds;
		private readonly Button[] subButtons = new Button[2];
		private readonly List<Button> bagCells = new List<Button>();
		private readonly List<Button> appraiseButtons = new List<Button>();
		private readonly VisualElement bagView;
		private readonly VisualElement forgeView;
		private readonly Label gearSummary;
		private readonly ForgePanelController forge;
		private readonly VisualElement appraiseRows;
		private readonly Button bulkMergeButton;

		public ItemPageController(
			VisualElement page,
			IdleSession session,
			UIContentSO content,
			GearVisualPresenter gearVisualPresenter,
			VisualTreeAsset bagCellAsset,
			VisualTreeAsset forgeKindAsset,
			VisualTreeAsset rowButtonAsset,
			Func<int> selectedHeroId,
			Action writeDown,
			Action requestRender,
			Action<string, float> showFeedback,
			Action<VisualElement, Func<string>> hookTooltip,
			float feedbackSeconds,
			long lockHoldMilliseconds)
		{
			this.session = session;
			this.content = content;
			this.gearVisualPresenter = gearVisualPresenter;
			this.bagCellAsset = bagCellAsset;
			this.rowButtonAsset = rowButtonAsset;
			this.selectedHeroId = selectedHeroId;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.hookTooltip = hookTooltip;
			this.feedbackSeconds = feedbackSeconds;

			subButtons[0] = page.RequireQ<Button>("bag-subtab");
			subButtons[0].clicked += () => OpenSubPage(0);
			subButtons[1] = page.RequireQ<Button>("forge-subtab");
			subButtons[1].clicked += () => OpenSubPage(1);

			bagView = page.RequireQ<VisualElement>("bag-view");
			gearSummary = page.RequireQ<Label>("gear-summary");
			VisualElement bagGrid = page.RequireQ<VisualElement>("bag-grid");
			for (int index = 0; index < content.BagSlotCount; index++)
			{
				int captured = index;
				Button cell = AddBagCell(bagGrid);
				hookTooltip(cell, () => BagTip(captured));
				HookLongPress(cell, lockHoldMilliseconds, () => ToggleLock(captured));
				bagCells.Add(cell);
			}

			page.RequireQ<Button>("sort-button").clicked += SortBag;
			bulkMergeButton = page.RequireQ<Button>("bulk-merge-button");
			bulkMergeButton.clicked += MergeAll;
			forgeView = page.RequireQ<VisualElement>("forge-view");
			forge = new ForgePanelController(
				page, session, content, gearVisualPresenter, forgeKindAsset,
				writeDown, requestRender, showFeedback, feedbackSeconds);

			Label appraiseCap = page.RequireQ<Label>("appraise-cap");
			appraiseCap.style.display = DisplayStyle.None;
			appraiseRows = page.RequireQ<VisualElement>("appraise-rows");
			appraiseRows.style.display = DisplayStyle.None;
			OpenSubPage(0, false);
		}

		public void Render(IdleSnapshot snapshot)
		{
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;
			gearSummary.text = content.BagSummaryText(snapshot.Bag.Length, snapshot.BagCapacity, full);
			gearSummary.EnableInClassList("idle-warn", full);

			for (int index = 0; index < bagCells.Count; index++)
			{
				RenderBagCell(snapshot, index);
			}

			bulkMergeButton.text = content.BulkMergeText(snapshot.MergeCount);
			bulkMergeButton.SetEnabled(IdleAdvice.MergeableCount(snapshot) > 0);
			forge.Render(snapshot);
			RenderAppraise(snapshot);
		}

		public void Equip(int bagIndex)
		{
			session.Send(new IdleEquipIntent(selectedHeroId(), bagIndex));
			writeDown();
			requestRender();
		}

		public string BagTip(int index)
		{
			IdleSnapshot snapshot = session.Capture();
			if (index < 0 || index >= snapshot.Bag.Length)
			{
				return string.Empty;
			}

			IdleItem item = snapshot.Bag[index];
			int heroId = selectedHeroId();
			IdleItem worn = heroId >= 0 ? session.WornOf(heroId, (int)item.Slot) : default;
			string wornText = worn.IsEmpty
				? content.NoWornGearText
				: content.WornGearSummaryText(session.GearMultiplierOf(worn));
			string tip = content.BagTipText(
				content.GearSlotName((int)item.Slot),
				session.GearMultiplierOf(item), wornText);
			return item.Locked ? tip + content.LockedTipSuffix : tip;
		}

		public string WornTip(int slot)
		{
			int heroId = selectedHeroId();
			IdleItem item = heroId >= 0 ? session.WornOf(heroId, slot) : default;
			return item.IsEmpty
				? content.WornEmptyTipText(content.GearSlotName(slot))
				: content.WornTipText(content.GearSlotName(slot),
					session.GearMultiplierOf(item));
		}

		private void OpenSubPage(int which, bool render = true)
		{
			bagView.style.display = which == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			forgeView.style.display = which == 1 ? DisplayStyle.Flex : DisplayStyle.None;
			for (int index = 0; index < subButtons.Length; index++)
			{
				subButtons[index].EnableInClassList("idle-subtab--on", index == which);
			}

			if (render)
			{
				requestRender();
			}
		}

		private void RenderBagCell(IdleSnapshot snapshot, int index)
		{
			Button cell = bagCells[index];
			if (index >= snapshot.BagCapacity)
			{
				cell.style.display = DisplayStyle.None;
				return;
			}

			cell.style.display = DisplayStyle.Flex;
			VisualElement icon = cell.RequireQ<VisualElement>("bag-icon");
			Label potential = cell.RequireQ<Label>("bag-potential");
			if (index >= snapshot.Bag.Length)
			{
				cell.text = string.Empty;
				icon.style.display = DisplayStyle.None;
				potential.text = string.Empty;
				cell.SetEnabled(false);
				gearVisualPresenter.SetTierOutline(cell, 0);
				return;
			}

			IdleItem item = snapshot.Bag[index];
			cell.text = string.Empty;
			icon.style.display = DisplayStyle.Flex;
			gearVisualPresenter.SetSprite(icon, (int)item.Slot, item.Tier);
			potential.text = content.ItemPotentialText(item.IsRaw, item.PotentialValue);
			cell.SetEnabled(true);
			cell.EnableInClassList("idle-bag-cell--locked", item.Locked);
			gearVisualPresenter.SetTierOutline(cell, item.Tier);
		}

		private void RenderAppraise(IdleSnapshot snapshot)
		{
			if (appraiseButtons.Count != snapshot.DroppedByTier.Length)
			{
				appraiseRows.Clear();
				appraiseButtons.Clear();
				for (int tier = 1; tier <= snapshot.DroppedByTier.Length; tier++)
				{
					int captured = tier;
					appraiseButtons.Add(AddRowButton(() => Appraise(captured)));
				}
			}

			for (int tier = 1; tier <= appraiseButtons.Count; tier++)
			{
				long count = snapshot.DroppedByTier[tier - 1];
				IdleAppraiseView appraisal = session.ViewAppraisal(tier);
				appraiseButtons[tier - 1].text = content.AppraiseRowText(tier,
					BigNumberText.Format(count), BigNumberText.Format(appraisal.Cost),
					(appraisal.Block == AppraiseBlock.TierTooLow) == false);
				appraiseButtons[tier - 1].SetEnabled(appraisal.Block == AppraiseBlock.None);
			}
		}

		/// <summary>길게 누르기. 손을 떼거나 벗어나면 취소. 마우스와 손가락 같음</summary>
		private static void HookLongPress(VisualElement target, long holdMilliseconds, Action fired)
		{
			IVisualElementScheduledItem pending = null;
			target.RegisterCallback<PointerDownEvent>(_ =>
			{
				pending?.Pause();
				pending = target.schedule.Execute(fired).StartingIn(holdMilliseconds);
			});
			target.RegisterCallback<PointerUpEvent>(_ => pending?.Pause());
			target.RegisterCallback<PointerLeaveEvent>(_ => pending?.Pause());
			target.RegisterCallback<PointerCancelEvent>(_ => pending?.Pause());
		}

		private void ToggleLock(int bagIndex)
		{
			IdleSnapshot snapshot = session.Capture();
			if (bagIndex < 0 || bagIndex >= snapshot.Bag.Length)
			{
				return;
			}

			if (session.Send(new IdleLockItemIntent(bagIndex, snapshot.Bag[bagIndex].Locked == false)))
			{
				writeDown();
			}

			requestRender();
		}

		private void SortBag()
		{
			session.Send(new IdleSortBagIntent());
			writeDown();
			requestRender();
		}

		private void MergeAll()
		{
			int merged = 0;
			IdleSnapshot snapshot = session.Capture();
			for (int tier = 1; tier <= snapshot.TierCeiling + 1; tier++)
			{
				while (session.Send(new IdleMergeIntent(tier, IdleItemSlot.Head)))
				{
					merged++;
				}
			}

			if (merged > 0)
			{
				showFeedback(content.MergeAllFeedbackText(merged), feedbackSeconds);
				writeDown();
			}

			requestRender();
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				showFeedback(content.AppraiseFeedbackText(
					roll.Tier, roll.Value, roll.Replaced), feedbackSeconds);
				writeDown();
			}

			requestRender();
		}

		private Button AddBagCell(VisualElement parent)
		{
			TemplateContainer tree = bagCellAsset.Instantiate();
			Button cell = tree.RequireQ<Button>("bag-cell");
			cell.RemoveFromHierarchy();
			cell.text = string.Empty;
			parent.Add(cell);
			return cell;
		}

		private Button AddRowButton(Action clicked)
		{
			TemplateContainer tree = rowButtonAsset.Instantiate();
			Button button = tree.RequireQ<Button>("row");
			button.RemoveFromHierarchy();
			button.clicked += clicked;
			appraiseRows.Add(button);
			return button;
		}
	}
}
