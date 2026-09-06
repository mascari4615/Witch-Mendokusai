using System;
using UnityEngine;
using UnityEngine.Serialization;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle
{
	// UIContentSO.cs 의 Text 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 글자 조립.
	public sealed partial class UIContentSO
	{
		public string BagSummaryText(int count, int capacity, bool full) =>
			string.Format(bagSummaryFormat, count, capacity, full ? bagFullSuffix : string.Empty);

		public string ItemPotentialText(bool raw, double potential) => raw
			? unidentifiedText
			: string.Format(potentialFormat, potential);

		public string GearPotentialText(bool raw, double potential) => raw
			? unidentifiedText
			: string.Format(gearPotentialFormat, potential);

		public string BulkMergeText(int count) => string.Format(bulkMergeFormat, count);

		public string ForgeKindText(int tier, int count) => string.Format(forgeKindFormat, tier, count);

		public string ForgeCellText(int tier) => string.Format(forgeCellFormat, tier);

		public string ForgeResultText(int tier) => string.Format(forgeResultFormat, tier);

		public string ForgeSelectionText(int tier, int count, int needed) =>
			string.Format(forgeSelectionFormat, tier, count, needed);

		public string ForgeEmptyHintText(int needed) => string.Format(forgeEmptyHintFormat, needed);

		public string SalvageTitleText(int count, string gold) => string.Format(salvageTitleFormat, count, gold);

		public string SalvageCountText(int count) => string.Format(salvageCountFormat, count);

		public string SalvageAllText => salvageAllText;

		public string SalvageFeedbackText(int count, string gold) => string.Format(salvageFeedbackFormat, count, gold);

		public string LockedTipSuffix => lockedTipSuffix;

		public string EquippedGearText(IdleItem item) => item.IsEmpty
			? noEquippedGearText
			: item.IsRaw ? rawEquippedGearText : appraisedEquippedGearText;

		public string DungeonRowText(string name, long tickets, long hours, long minutes) =>
			string.Format(dungeonRowFormat, name, tickets, hours, minutes);

		public string DungeonTicketText(long left, long cap) => string.Format(dungeonTicketFormat, left, cap);

		public string DungeonRefillText(string span) => string.Format(dungeonRefillFormat, span);

		public string DungeonEnterText => dungeonEnterText;

		public string DungeonSweepText(long left) => string.Format(dungeonSweepFormat, left);

		public string DungeonClosedText => dungeonClosedText;

		public string DungeonGoldRewardText(string gold) => string.Format(dungeonGoldRewardFormat, gold);

		public string DungeonBossRewardText(long shards, long gear, int tier) =>
			string.Format(dungeonBossRewardFormat, shards, gear, tier);

		public string DungeonGearRewardText(long gear, int tier) => string.Format(dungeonGearRewardFormat, gear, tier);

		public string DungeonFeedbackText(string name, int runs, string got) =>
			string.Format(dungeonFeedbackFormat, name, runs, got);

		public string AwaySpanText(double seconds) => string.Format(awaySpanFormat, DescribeSpan(seconds));

		public string AwayWarningText(double capSeconds, double lostSeconds) =>
			string.Format(awayWarningFormat, DescribeSpan(capSeconds), DescribeSpan(lostSeconds));

		public string SelectHeroBeforeGearText => selectHeroBeforeGearText;

		public string AppraiseRowText(int tier, string count, string cost, bool available) => available
			? string.Format(appraiseAvailableFormat, tier, count, cost)
			: string.Format(appraiseUnavailableFormat, tier, count);

		public string DiscoverySummaryText(int score, double multiplier, int owned, int total) =>
			string.Format(discoverySummaryFormat, score, multiplier, owned, total);

		public string DiscoveryHeroText(string name, string stars, string grade, string axis) =>
			string.Format(discoveryOwnedHeroFormat, name, stars, grade, axis);

		public string DiscoveryHiddenHeroText(string grade) => string.Format(discoveryHiddenHeroFormat, grade);

		public string BagUpgradeText(int slots, string cost) => string.Format(bagUpgradeFormat, slots, cost);

		public string BagUpgradeMaxText => bagUpgradeMaxText;

		public string BagResetNoteText(int capacity) => string.Format(bagResetNoteFormat, capacity);

		public string PullAvailableText(string cost, long stoneCost, long stones) =>
			string.Format(pullAvailableFormat, cost, stoneCost, stones);

		public string PullNoStoneText(long stones) => string.Format(pullNoStoneFormat, stones);

		public string PullNoGoldText(string cost) => string.Format(pullNoGoldFormat, cost);

		public string PullOddsText(double legend, double epic, double rare, int pity) =>
			string.Format(pullOddsFormat, legend, epic, rare, pity);

		public string PullBatchText(int count, string cost, long stoneCost, string floorGrade) =>
			string.Format(pullBatchFormat, count, cost, stoneCost, floorGrade);

		public string PullBatchFeedbackText(int count, int legend, int epic, int rare, int newFaces) =>
			string.Format(pullBatchFeedbackFormat, count, legend, epic, rare, newFaces);

		public string PityCounterText(int pullsToPity) => string.Format(pityCounterFormat, pullsToPity);

		public string PickupText(string name, double weight, string span) => string.Format(pickupFormat, name, weight, span);

		public string PickupNoneText => pickupNoneText;

		public string FreeBoxReadyText(long stones) => string.Format(freeBoxReadyFormat, stones);

		public string FreeBoxWaitText(string span) => string.Format(freeBoxWaitFormat, span);

		public string FreeBoxFeedbackText(long stones) => string.Format(freeBoxFeedbackFormat, stones);

		public string OddsButtonText => oddsButtonText;

		public string OddsRowText(string grade, double chance) => string.Format(oddsRowFormat, grade, chance);

		public string OddsPityText(int pullsToPity) => string.Format(oddsPityFormat, pullsToPity);

		public string OddsPickupText(string name, double weight) => string.Format(oddsPickupFormat, name, weight);

		public string OddsBatchText(int count, string floorGrade) => string.Format(oddsBatchFormat, count, floorGrade);

		public string PrestigeSummaryText(long points, long award, double multiplier) =>
			string.Format(prestigeSummaryFormat, points, award, multiplier);

		public string PrestigeButtonText(long award, int nextStage) => award > 0L
			? string.Format(prestigeAvailableFormat, award)
			: string.Format(prestigeLockedFormat, nextStage);

		public string ProducerSummaryText(string income) => string.Format(producerSummaryFormat, income);

		public string ProducerRowText(int kind, long owned, string output, string nextCost) =>
			string.Format(producerRowFormat, kind, owned, output, nextCost);

		public string MapStageText(int stage, bool current, bool best) => string.Format(mapStageFormat, stage,
			current ? mapCurrentSuffix : string.Empty, best ? mapBestSuffix : string.Empty);

		public string ScenePlaceholderText(bool shop) => shop ? shopScenePlaceholder : labScenePlaceholder;

		public string VolleyTargetFeedback => volleyTargetFeedback;

		public string VolleyDragHint => volleyDragHint;

		public string VolleyResolvedFeedback => volleyResolvedFeedback;

		public string SupplyFeedbackText(double seconds, double multiplier) =>
			string.Format(supplyFeedbackFormat, seconds, multiplier);

		public string AppraiseCardFeedbackText(int tier, double value, bool replaced) =>
			string.Format(appraiseCardFeedbackFormat, tier, value, replaced ? appraiseReplacedSuffix : string.Empty);

		public string AppraiseCardEmptyFeedback => appraiseCardEmptyFeedback;

		public string NextStageFeedback => nextStageFeedback;

		public string StatRaisedFeedbackText(string name, string before, string after, string spent) =>
			string.Format(statRaisedFeedbackFormat, name, before, after, spent);

		public string MergeFeedbackText(string slot, int tier) => string.Format(mergeFeedbackFormat, slot, tier, tier + 1);

		public string MergeAllFeedbackText(int count) => string.Format(mergeAllFeedbackFormat, count);

		public string AppraiseFeedbackText(int tier, double value, bool replaced) =>
			string.Format(appraiseFeedbackFormat, tier, value, replaced ? appraiseReplacedSuffix : string.Empty);

		public string PullFeedbackText(string grade, string name, bool isNew, bool byPity) =>
			string.Format(pullFeedbackFormat, grade, name, isNew ? newHeroSuffix : string.Empty, byPity ? pitySuffix : string.Empty);

		public string PartyFullFeedback => partyFullFeedback;

		public string PrestigeFeedback => prestigeFeedback;

		public string StatSelectHeroTip => statSelectHeroTip;

		public string StatMaxTipText(string name) => string.Format(statMaxTipFormat, name);

		public string StatWaitTipText(double seconds) => string.Format(statWaitTipFormat, seconds);

		public string StatTipText(string name, int amount, string current, string next, string cost, string wait) =>
			string.Format(statTipFormat, name, amount, current, next, cost, wait);

		public string BagTipText(string slot, double multiplier, string worn) =>
			string.Format(bagTipFormat, slot, multiplier, worn);

		public string WornGearSummaryText(double multiplier) =>
			string.Format(wornGearSummaryFormat, multiplier);

		public string NoWornGearText => noWornGearText;

		public string WornEmptyTipText(string slot) => string.Format(wornEmptyTipFormat, slot);

		public string WornTipText(string slot, double multiplier) =>
			string.Format(wornTipFormat, slot, multiplier);

		public string OperationCodeText(int stage) => string.Format(operationCodeFormat, stage);

		public string CostText(double cost, double maximum) => string.Format(costFormat, cost, maximum);

		public string BossHealthText(int stage, double ratio) => string.Format(bossHealthFormat, stage, ratio);

		public string WaveText(int current, int total) => string.Format(waveFormat, current, total);

		public string PartySeatText(string seat, string heroName) => string.Format(partySeatFormat, seat, heroName);

		public string EmptyPartySeatText(string seat) => string.Format(emptyPartySeatFormat, seat);

		public string LevelText(int level) => string.Format(levelFormat, level);

		public string UpgradeButtonText(int amount, string cost) => string.Format(upgradeButtonFormat, amount, cost);

		public string GainText(string amount) => string.Format(gainFormat, amount);

		public string HeroChoiceText(string name, int stars, int level, string axis) =>
			string.Format(heroChoiceFormat, name, StarsText(stars), level, axis);

		public string PopupPageText(int page, int pageCount) => string.Format(popupPageFormat, page, pageCount);

		public string TabButtonText(int index) => string.Format(tabButtonFormat, TabName(index), TabCaption(index));

		public string StarsText(int count) => count <= 0 ? string.Empty : starPrefix + new string(starCharacter, count);

		public string VolleyMissFeedback => volleyMissFeedback;

		public string VolleyTapHint => volleyTapHint;

		public string GachaTitleText(int count) => string.Format(gachaTitleFormat, count);

		public string GachaSummaryText(int count, int legend, int epic, int newFaces) =>
			string.Format(gachaSummaryFormat, count, legend, epic, newFaces);

		public string GachaSkipText => gachaSkipText;

		public string GachaCloseText => gachaCloseText;

		public string GachaNewBadge => gachaNewBadge;

		public string GachaPityBadge => gachaPityBadge;

		public string DescribeSpan(double seconds)
		{
			if (seconds < 60d)
			{
				return string.Format(secondsSpanFormat, seconds);
			}

			if (seconds < 3600d)
			{
				return string.Format(minutesSpanFormat, seconds / 60d);
			}

			return string.Format(hoursSpanFormat, seconds / 3600d);
		}

		public string AdviceText(IdleStep step, double amount, string span)
		{
			switch (step)
			{
				case IdleStep.Prestige: return string.Format(prestigeAdviceFormat, (long)amount);
				case IdleStep.BuyProducer: return buyProducerAdvice;
				case IdleStep.Raise: return raiseAdvice;
				case IdleStep.Merge: return mergeAdvice;
				case IdleStep.Wear: return wearAdvice;
				case IdleStep.Pull: return pullAdvice;
				case IdleStep.Seat: return seatAdvice;
				case IdleStep.BagFull: return bagFullAdvice;
				case IdleStep.Tap: return tapAdvice;
				default: return amount > 0d && double.IsInfinity(amount) == false
					? string.Format(waitAdviceFormat, span)
					: waitForCostAdvice;
			}
		}

		public string StatValueText(IdleUpgradeKind kind, double value)
		{
			switch (kind)
			{
				case IdleUpgradeKind.AttackSpeed: return string.Format(attackSpeedValueFormat, value);
				case IdleUpgradeKind.Defense:
				case IdleUpgradeKind.CriticalChance:
				case IdleUpgradeKind.Recovery: return string.Format(percentValueFormat, value);
				case IdleUpgradeKind.CriticalDamage: return string.Format(criticalDamageValueFormat, value);
				default: return BigNumberText.Format(value);
			}
		}
	}
}

