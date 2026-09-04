using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>공방 판. 가방의 등급별 개수를 세고, 고른 등급을 합쳐 한 단계 위로</summary>
	public sealed class ForgePanelController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly GearVisualPresenter gearVisualPresenter;
		private readonly VisualTreeAsset forgeKindAsset;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly float feedbackSeconds;
		private readonly List<Label> cells = new List<Label>();
		private readonly List<Button> kindButtons = new List<Button>();
		private readonly List<int> kindKeys = new List<int>();
		private readonly VisualElement kinds;
		private readonly Label result;
		private readonly Label title;
		private readonly Button mergeButton;
		private int tier;

		public ForgePanelController(
			VisualElement page,
			IdleSession session,
			UIContentSO content,
			GearVisualPresenter gearVisualPresenter,
			VisualTreeAsset forgeKindAsset,
			Action writeDown,
			Action requestRender,
			Action<string, float> showFeedback,
			float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.gearVisualPresenter = gearVisualPresenter;
			this.forgeKindAsset = forgeKindAsset;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.feedbackSeconds = feedbackSeconds;

			kinds = page.Q<VisualElement>("forge-kinds");
			for (int index = 0; index < content.ForgeInputSlotCount; index++)
			{
				cells.Add(page.Q<Label>("forge-cell-" + index));
			}

			result = page.Q<Label>("forge-result");
			title = page.Q<Label>("forge-title");
			mergeButton = page.Q<Button>("forge-button");
			mergeButton.clicked += Merge;
		}

		public void Render(IdleSnapshot snapshot)
		{
			int[] counts = CountTiers(snapshot);
			EnsureKinds(PresentTiers(counts));

			for (int index = 0; index < kindButtons.Count; index++)
			{
				int kind = kindKeys[index];
				kindButtons[index].text = content.ForgeKindText(kind, counts[kind]);
				gearVisualPresenter.SetTierOutline(kindButtons[index], kind);
				kindButtons[index].EnableInClassList("idle-forge-kind--on", tier == kind);
			}

			int have = tier > 0 && tier < counts.Length ? counts[tier] : 0;
			int shown = have > snapshot.MergeCount ? snapshot.MergeCount : have;
			for (int index = 0; index < cells.Count; index++)
			{
				bool filled = index < shown;
				cells[index].text = filled ? content.ForgeCellText(tier) : string.Empty;
				gearVisualPresenter.SetTierOutline(cells[index], filled ? tier : 0);
			}

			bool ready = tier > 0 && have >= snapshot.MergeCount;
			result.text = tier > 0 ? content.ForgeResultText(tier + 1) : string.Empty;
			gearVisualPresenter.SetTierOutline(result, tier > 0 ? tier + 1 : 0);
			result.EnableInClassList("idle-forge-cell--ready", ready);
			title.text = tier > 0
				? content.ForgeSelectionText(tier, have, snapshot.MergeCount)
				: content.ForgeEmptyHintText(snapshot.MergeCount);
			mergeButton.SetEnabled(ready);
		}

		private static int[] CountTiers(IdleSnapshot snapshot)
		{
			int[] counts = new int[snapshot.TierCeiling + 2];
			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				int owned = snapshot.Bag[index].Tier;
				if (owned >= 0 && owned < counts.Length)
				{
					counts[owned]++;
				}
			}

			return counts;
		}

		private static List<int> PresentTiers(int[] counts)
		{
			List<int> keys = new List<int>();
			for (int owned = 0; owned < counts.Length; owned++)
			{
				if (counts[owned] > 0)
				{
					keys.Add(owned);
				}
			}

			return keys;
		}

		/// <summary>등급 단추는 가방에 있는 등급만. 같은 목록이면 다시 안 짓는다</summary>
		private void EnsureKinds(List<int> keys)
		{
			if (kindKeys.Count == keys.Count && KeysDiffer(keys) == false)
			{
				return;
			}

			kinds.Clear();
			kindButtons.Clear();
			kindKeys.Clear();
			for (int index = 0; index < keys.Count; index++)
			{
				int kind = keys[index];
				kindKeys.Add(kind);
				kindButtons.Add(AddKind(() => Pick(kind)));
			}
		}

		private bool KeysDiffer(List<int> keys)
		{
			for (int index = 0; index < keys.Count; index++)
			{
				if (keys[index] != kindKeys[index])
				{
					return true;
				}
			}

			return false;
		}

		private void Pick(int kind)
		{
			tier = kind;
			requestRender();
		}

		private void Merge()
		{
			if (tier <= 0)
			{
				return;
			}

			if (session.Send(new IdleMergeIntent(tier, IdleItemSlot.Head)))
			{
				showFeedback(content.MergeFeedbackText(
					content.GearSlotName((int)IdleItemSlot.Head), tier), feedbackSeconds);
				writeDown();
			}

			requestRender();
		}

		private Button AddKind(Action clicked)
		{
			TemplateContainer tree = forgeKindAsset.Instantiate();
			Button kind = tree.Q<Button>("forge-kind");
			kind.RemoveFromHierarchy();
			kind.clicked += clicked;
			kinds.Add(kind);
			return kind;
		}
	}
}
