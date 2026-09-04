using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>아이템 화면의 가방, 공방, 감정 표시와 명령을 맡는다.</summary>
	public sealed class ItemPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly GearVisualPresenter gearVisualPresenter;
		private readonly VisualTreeAsset bagCellAsset;
		private readonly VisualTreeAsset forgeKindAsset;
		private readonly VisualTreeAsset rowButtonAsset;
		private readonly Func<int> selectedHeroId;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly Action<VisualElement, Func<string>> hookTooltip;
		private readonly float feedbackSeconds;
		private readonly Button[] subButtons = new Button[2];
		private readonly List<Button> bagCells = new List<Button>();
		private readonly List<Label> forgeCells = new List<Label>();
		private readonly List<Button> forgeKindButtons = new List<Button>();
		private readonly List<int> forgeKindKeys = new List<int>();
		private readonly List<Button> appraiseButtons = new List<Button>();
		private readonly VisualElement bagView;
		private readonly VisualElement forgeView;
		private readonly Label gearSummary;
		private readonly VisualElement forgeKinds;
		private readonly Label forgeResult;
		private readonly Label forgeTitle;
		private readonly Button forgeButton;
		private readonly VisualElement appraiseRows;
		private readonly Button bulkMergeButton;
		private int forgeTier;

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
			float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.gearVisualPresenter = gearVisualPresenter;
			this.bagCellAsset = bagCellAsset;
			this.forgeKindAsset = forgeKindAsset;
			this.rowButtonAsset = rowButtonAsset;
			this.selectedHeroId = selectedHeroId;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.hookTooltip = hookTooltip;
			this.feedbackSeconds = feedbackSeconds;

			subButtons[0] = page.Q<Button>("bag-subtab");
			subButtons[0].clicked += () => OpenSubPage(0);
			subButtons[1] = page.Q<Button>("forge-subtab");
			subButtons[1].clicked += () => OpenSubPage(1);

			bagView = page.Q<VisualElement>("bag-view");
			gearSummary = page.Q<Label>("gear-summary");
			VisualElement bagGrid = page.Q<VisualElement>("bag-grid");
			for (int index = 0; index < content.BagSlotCount; index++)
			{
				int captured = index;
				Button cell = AddBagCell(bagGrid);
				hookTooltip(cell, () => BagTip(captured));
				bagCells.Add(cell);
			}

			bulkMergeButton = page.Q<Button>("bulk-merge-button");
			bulkMergeButton.clicked += MergeAll;
			forgeView = page.Q<VisualElement>("forge-view");
			forgeKinds = page.Q<VisualElement>("forge-kinds");
			for (int index = 0; index < content.ForgeInputSlotCount; index++)
			{
				forgeCells.Add(page.Q<Label>("forge-cell-" + index));
			}

			forgeResult = page.Q<Label>("forge-result");
			forgeTitle = page.Q<Label>("forge-title");
			forgeButton = page.Q<Button>("forge-button");
			forgeButton.clicked += MergeForge;

			Label appraiseCap = page.Q<Label>("appraise-cap");
			appraiseCap.style.display = DisplayStyle.None;
			appraiseRows = page.Q<VisualElement>("appraise-rows");
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
			RenderForge(snapshot);
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
				: content.WornGearSummaryText(worn.Tier, session.GearMultiplierOf(worn));
			return content.BagTipText(
				content.GearSlotName((int)item.Slot), item.Tier,
				session.GearMultiplierOf(item), wornText);
		}

		public string WornTip(int slot)
		{
			int heroId = selectedHeroId();
			IdleItem item = heroId >= 0 ? session.WornOf(heroId, slot) : default;
			return item.IsEmpty
				? content.WornEmptyTipText(content.GearSlotName(slot))
				: content.WornTipText(content.GearSlotName(slot), item.Tier,
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
			VisualElement icon = cell.Q<VisualElement>("bag-icon");
			Label potential = cell.Q<Label>("bag-potential");
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
			gearVisualPresenter.SetTierOutline(cell, item.Tier);
		}

		private void RenderForge(IdleSnapshot snapshot)
		{
			int[] counts = CountTiers(snapshot);
			List<int> keys = PresentTiers(counts);
			EnsureForgeKinds(keys);

			for (int index = 0; index < forgeKindButtons.Count; index++)
			{
				int tier = forgeKindKeys[index];
				forgeKindButtons[index].text = content.ForgeKindText(tier, counts[tier]);
				gearVisualPresenter.SetTierOutline(forgeKindButtons[index], tier);
				forgeKindButtons[index].EnableInClassList("idle-forge-kind--on", forgeTier == tier);
			}

			int have = forgeTier > 0 && forgeTier < counts.Length ? counts[forgeTier] : 0;
			int shown = have > snapshot.MergeCount ? snapshot.MergeCount : have;
			for (int index = 0; index < forgeCells.Count; index++)
			{
				bool filled = index < shown;
				forgeCells[index].text = filled ? content.ForgeCellText(forgeTier) : string.Empty;
				gearVisualPresenter.SetTierOutline(forgeCells[index], filled ? forgeTier : 0);
			}

			bool ready = forgeTier > 0 && have >= snapshot.MergeCount;
			forgeResult.text = forgeTier > 0 ? content.ForgeResultText(forgeTier + 1) : string.Empty;
			gearVisualPresenter.SetTierOutline(forgeResult, forgeTier > 0 ? forgeTier + 1 : 0);
			forgeResult.EnableInClassList("idle-forge-cell--ready", ready);
			forgeTitle.text = forgeTier > 0
				? content.ForgeSelectionText(forgeTier, have, snapshot.MergeCount)
				: content.ForgeEmptyHintText(snapshot.MergeCount);
			forgeButton.SetEnabled(ready);
		}

		private static int[] CountTiers(IdleSnapshot snapshot)
		{
			int[] counts = new int[snapshot.TierCeiling + 2];
			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				int tier = snapshot.Bag[index].Tier;
				if (tier >= 0 && tier < counts.Length)
				{
					counts[tier]++;
				}
			}

			return counts;
		}

		private static List<int> PresentTiers(int[] counts)
		{
			List<int> keys = new List<int>();
			for (int tier = 0; tier < counts.Length; tier++)
			{
				if (counts[tier] > 0)
				{
					keys.Add(tier);
				}
			}

			return keys;
		}

		private void EnsureForgeKinds(List<int> keys)
		{
			if (forgeKindKeys.Count == keys.Count && KeysDiffer(keys) == false)
			{
				return;
			}

			forgeKinds.Clear();
			forgeKindButtons.Clear();
			forgeKindKeys.Clear();
			for (int index = 0; index < keys.Count; index++)
			{
				int tier = keys[index];
				forgeKindKeys.Add(tier);
				forgeKindButtons.Add(AddForgeKind(() => PickForge(tier)));
			}
		}

		private bool KeysDiffer(List<int> keys)
		{
			for (int index = 0; index < keys.Count; index++)
			{
				if (keys[index] != forgeKindKeys[index])
				{
					return true;
				}
			}

			return false;
		}

		private void PickForge(int tier)
		{
			forgeTier = tier;
			requestRender();
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

		private void MergeForge()
		{
			if (forgeTier <= 0)
			{
				return;
			}

			if (session.Send(new IdleMergeIntent(forgeTier, IdleItemSlot.Head)))
			{
				showFeedback(content.MergeFeedbackText(
					content.GearSlotName((int)IdleItemSlot.Head), forgeTier), feedbackSeconds);
				writeDown();
			}

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
			Button cell = tree.Q<Button>("bag-cell");
			cell.RemoveFromHierarchy();
			cell.text = string.Empty;
			parent.Add(cell);
			return cell;
		}

		private Button AddForgeKind(Action clicked)
		{
			TemplateContainer tree = forgeKindAsset.Instantiate();
			Button kind = tree.Q<Button>("forge-kind");
			kind.RemoveFromHierarchy();
			kind.clicked += clicked;
			forgeKinds.Add(kind);
			return kind;
		}

		private Button AddRowButton(Action clicked)
		{
			TemplateContainer tree = rowButtonAsset.Instantiate();
			Button button = tree.Q<Button>("row");
			button.RemoveFromHierarchy();
			button.clicked += clicked;
			appraiseRows.Add(button);
			return button;
		}
	}
}
