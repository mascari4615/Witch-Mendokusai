using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>인형 화면의 파티 좌석, 성장 수치, 착용 장비를 맡는다.</summary>
	public sealed class DollPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly HeroVisualPresenter heroVisualPresenter;
		private readonly GearVisualPresenter gearVisualPresenter;
		private readonly Func<int> selectedHeroId;
		private readonly Func<int> selectedGearSeat;
		private readonly Func<int> selectingPartySeat;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action playGood;
		private readonly List<Button> partyButtons = new List<Button>();
		private readonly List<Button> wornCells = new List<Button>();
		private readonly IdleItem[] worn = new IdleItem[IdleGear.SLOT_COUNT];
		private readonly Label dollName;
		private readonly Label[] statValues;
		private readonly Label[] statLevels;
		private readonly Button[] statButtons;
		private readonly Label statFeedback;
		private int statFeedbackVersion;

		public DollPageController(
			VisualElement page,
			IdleSession session,
			UIContentSO content,
			HeroVisualPresenter heroVisualPresenter,
			GearVisualPresenter gearVisualPresenter,
			Func<int> selectedHeroId,
			Func<int> selectedGearSeat,
			Func<int> selectingPartySeat,
			Action<int> selectPartySeat,
			Action<int> openGear,
			Func<int, string> wornTip,
			Action<VisualElement, Func<string>> hookTooltip,
			Action writeDown,
			Action requestRender,
			Action playGood)
		{
			this.session = session;
			this.content = content;
			this.heroVisualPresenter = heroVisualPresenter;
			this.gearVisualPresenter = gearVisualPresenter;
			this.selectedHeroId = selectedHeroId;
			this.selectedGearSeat = selectedGearSeat;
			this.selectingPartySeat = selectingPartySeat;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.playGood = playGood;

			statValues = new Label[content.StatCount];
			statLevels = new Label[content.StatCount];
			statButtons = new Button[content.StatCount];
			for (int slot = 0; slot < IdleHeroes.PARTY_SLOTS; slot++)
			{
				int captured = slot;
				Button seat = page.Q<Button>("seat-" + slot);
				seat.clicked += () => selectPartySeat(captured);
				partyButtons.Add(seat);
			}

			dollName = page.Q<Label>("doll-name");
			statFeedback = page.Q<Label>("stat-feedback");
			statFeedback.style.visibility = Visibility.Hidden;
			for (int stat = 0; stat < content.StatCount; stat++)
			{
				int capturedStat = stat;
				Label name = page.Q<Label>("stat-name-" + stat);
				statValues[stat] = page.Q<Label>("stat-value-" + stat);
				statLevels[stat] = page.Q<Label>("stat-level-" + stat);
				name.text = content.StatName(stat);

				Button button = page.Q<Button>("stat-" + stat + "-upgrade");
				button.clicked += () => Raise((IdleUpgradeKind)capturedStat, content.StatUpgradeAmount);
				hookTooltip(button, () => StatTip((IdleUpgradeKind)capturedStat, content.StatUpgradeAmount));
				statButtons[stat] = button;
			}

			for (int slot = 0; slot < content.GearSlotCount; slot++)
			{
				int captured = slot;
				Button cell = page.Q<Button>("worn-" + slot);
				cell.clicked += () => openGear(captured);
				hookTooltip(cell, () => wornTip(captured));
				wornCells.Add(cell);
			}
		}

		public void Render(IdleSnapshot snapshot)
		{
			RenderParty(snapshot);
			int heroId = selectedHeroId();
			dollName.text = heroId >= 0
				? content.GrowthTitle(IdleHeroes.KindOf(heroId).Name)
				: content.EmptySeatText;
			RenderStats(heroId);
			RenderWorn(heroId);
		}

		private void RenderParty(IdleSnapshot snapshot)
		{
			int selectingSeat = selectingPartySeat();
			int gearSeat = selectedGearSeat();
			for (int slot = 0; slot < partyButtons.Count; slot++)
			{
				int heroId = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				string tag = content.SeatText(IdleHeroes.IsMainSlot(slot));
				Button seat = partyButtons[slot];
				seat.text = string.Empty;
				VisualElement portrait = seat.Q<VisualElement>("seat-icon-" + slot);
				Label label = seat.Q<Label>("seat-label-" + slot);
				portrait.style.display = heroId >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
				label.text = heroId >= 0
					? content.PartySeatText(tag, IdleHeroes.KindOf(heroId).Name)
					: content.EmptyPartySeatText(tag);
				if (heroId >= 0)
				{
					heroVisualPresenter.SetPortrait(portrait, heroId);
				}

				seat.EnableInClassList("idle-party-seat--picking", selectingSeat == slot);
				seat.EnableInClassList("idle-party-seat--geared", selectingSeat < 0 && gearSeat == slot);
			}
		}

		private void RenderStats(int heroId)
		{
			for (int stat = 0; stat < content.StatCount; stat++)
			{
				IdleUpgradeKind kind = (IdleUpgradeKind)stat;
				IdleUpgradeView current = session.ViewHeroStat(heroId, kind, 1);
				statValues[stat].text = content.StatValueText(kind, current.CurrentValue);
				statLevels[stat].text = content.LevelText(current.Level);

				IdleUpgradeView purchase = session.ViewHeroStat(heroId, kind, content.StatUpgradeAmount);
				Button button = statButtons[stat];
				button.text = purchase.IsMaxed
					? content.MaxedText
					: content.UpgradeButtonText(BigNumberText.Format(purchase.NextCost));
				bool canAfford = heroId >= 0 && purchase.CanAfford;
				button.EnableInClassList("idle-stat-buy--ready", canAfford);
				button.EnableInClassList("idle-stat-buy--maxed", purchase.IsMaxed);
				button.SetEnabled(canAfford);
			}
		}

		private void RenderWorn(int heroId)
		{
			if (heroId >= 0)
			{
				session.CopyWornOf(heroId, worn);
			}
			else
			{
				Array.Clear(worn, 0, worn.Length);
			}

			for (int slot = 0; slot < wornCells.Count && slot < worn.Length; slot++)
			{
				IdleItem item = worn[slot];
				Button cell = wornCells[slot];
				cell.text = string.Empty;
				VisualElement icon = cell.Q<VisualElement>("worn-icon-" + slot);
				Label badge = cell.Q<Label>("worn-label-" + slot);
				icon.style.display = item.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
				badge.text = item.IsEmpty ? content.GearSlotName(slot) : string.Empty;
				if (item.IsEmpty == false)
				{
					gearVisualPresenter.SetSprite(icon, slot, item.Tier);
				}

				cell.EnableInClassList("idle-worn-cell--empty", item.IsEmpty);
				cell.SetEnabled(heroId >= 0);
				gearVisualPresenter.SetTierOutline(cell, item.IsEmpty ? 0 : item.Tier);
			}
		}

		private void Raise(IdleUpgradeKind kind, int amount)
		{
			int heroId = selectedHeroId();
			if (heroId < 0)
			{
				return;
			}

			IdleUpgradeView before = session.ViewHeroStat(heroId, kind, amount);
			double resourceBefore = session.Capture().Resource;
			bool raised = session.Send(new IdleRaiseUpgradeIntent(heroId, kind, amount));
			if (raised)
			{
				writeDown();
			}

			requestRender();
			if (raised)
			{
				IdleUpgradeView after = session.ViewHeroStat(heroId, kind, 1);
				ShowStatRaised(kind, amount, before.CurrentValue, after.CurrentValue,
					resourceBefore - session.Capture().Resource);
			}
		}

		private void ShowStatRaised(IdleUpgradeKind kind, int amount, double before, double after, double spent)
		{
			int stat = (int)kind;
			if (stat < 0 || stat >= statValues.Length)
			{
				return;
			}

			statFeedbackVersion++;
			playGood();
			int version = statFeedbackVersion;
			statFeedback.text = content.StatRaisedFeedbackText(
				content.StatName(stat), content.StatValueText(kind, before),
				content.StatValueText(kind, after), BigNumberText.Format(spent));
			statFeedback.style.visibility = Visibility.Visible;
			statFeedback.AddToClassList("idle-stat-feedback--shown");
			statValues[stat].AddToClassList("idle-stat-label--raised");
			statButtons[stat].AddToClassList("idle-stat-buy--raised");

			statFeedback.schedule.Execute(() =>
			{
				if (version == statFeedbackVersion)
				{
					statValues[stat].RemoveFromClassList("idle-stat-label--raised");
					statButtons[stat].RemoveFromClassList("idle-stat-buy--raised");
				}
			}).StartingIn(350L);

			statFeedback.schedule.Execute(() =>
			{
				if (version == statFeedbackVersion)
				{
					statFeedback.RemoveFromClassList("idle-stat-feedback--shown");
					statFeedback.style.visibility = Visibility.Hidden;
				}
			}).StartingIn(1200L);
		}

		private string StatTip(IdleUpgradeKind kind, int amount)
		{
			int heroId = selectedHeroId();
			if (heroId < 0)
			{
				return content.StatSelectHeroTip;
			}

			IdleUpgradeView view = session.ViewHeroStat(heroId, kind, amount);
			if (view.IsMaxed)
			{
				return content.StatMaxTipText(content.StatName((int)kind));
			}

			string wait = view.CanAfford || double.IsInfinity(view.SecondsToAfford)
				? string.Empty
				: content.StatWaitTipText(view.SecondsToAfford);
			return content.StatTipText(
				content.StatName((int)kind), amount,
				content.StatValueText(kind, view.CurrentValue), content.StatValueText(kind, view.NextValue),
				BigNumberText.Format(view.NextCost), wait);
		}
	}
}
