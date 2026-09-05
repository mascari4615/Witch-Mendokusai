using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleUIContent", menuName = "WM/Idle/UI Content")]
	public sealed class UIContentSO : ScriptableObject
	{
		[Serializable]
		private struct TabDefinition
		{
			public string Name;
			public string Caption;
			public bool Visible;
		}

		[SerializeField] private TabDefinition[] tabs = Array.Empty<TabDefinition>();
		[SerializeField] private string[] gearSlotNames = Array.Empty<string>();
		[SerializeField] private string[] statNames = Array.Empty<string>();
		[SerializeField] private string[] heroAxisNames = Array.Empty<string>();
		[SerializeField] private string[] heroGradeNames = Array.Empty<string>();
		[SerializeField] private string[] cardNames = Array.Empty<string>();
		[SerializeField] private string[] dungeonNames = Array.Empty<string>();
		[SerializeField] private string prestigeAdviceFormat;
		[SerializeField] private string buyProducerAdvice;
		[SerializeField] private string raiseAdvice;
		[SerializeField] private string mergeAdvice;
		[SerializeField] private string wearAdvice;
		[SerializeField] private string pullAdvice;
		[SerializeField] private string seatAdvice;
		[SerializeField] private string bagFullAdvice;
		[SerializeField] private string tapAdvice;
		[SerializeField] private string waitAdviceFormat;
		[SerializeField] private string waitForCostAdvice;
		[SerializeField] private string battleGradeFormat;
		[SerializeField] private string stageFormat;
		[SerializeField] private string repeatOnText;
		[SerializeField] private string repeatOffText;
		[SerializeField] private string goldAmountFormat;
		[SerializeField] private string goldIncomeFormat;
		[SerializeField] private string mainSeatText;
		[SerializeField] private string supportSeatText;
		[SerializeField] private string growthSuffix;
		[SerializeField] private string emptySeatText;
		[SerializeField] private string maxedText;
		[SerializeField] private string bagSummaryFormat;
		[SerializeField] private string bagFullSuffix;
		[SerializeField] private string unidentifiedText;
		[SerializeField] private string bulkMergeFormat;
		[SerializeField] private string forgeKindFormat;
		[SerializeField] private string forgeCellFormat;
		[SerializeField] private string forgeResultFormat;
		[SerializeField] private string forgeSelectionFormat;
		[SerializeField] private string forgeEmptyHintFormat;
		[SerializeField] private string salvageTitleFormat;
		[SerializeField] private string salvageCountFormat;
		[SerializeField] private string salvageAllText;
		[SerializeField] private string salvageFeedbackFormat;
		[SerializeField] private string lockedTipSuffix;
		[SerializeField] private string noEquippedGearText;
		[SerializeField] private string rawEquippedGearText;
		[SerializeField] private string appraisedEquippedGearText;
		[SerializeField] private string potentialFormat;
		[SerializeField] private string gearPotentialFormat;
		[SerializeField] private string dungeonRowFormat;
		[SerializeField] private string awaySpanFormat;
		[SerializeField] private string awayWarningFormat;
		[SerializeField] private string selectHeroBeforeGearText;
		[SerializeField] private string appraiseUnavailableFormat;
		[SerializeField] private string appraiseAvailableFormat;
		[SerializeField] private string codexSummaryFormat;
		[SerializeField] private string codexOwnedHeroFormat;
		[SerializeField] private string codexHiddenHeroFormat;
		[SerializeField] private string bagUpgradeFormat;
		[SerializeField] private string bagUpgradeMaxText;
		[SerializeField] private string bagResetNoteFormat;
		[SerializeField] private string pullAvailableFormat;
		[SerializeField] private string pullNoStoneFormat;
		[SerializeField] private string pullNoGoldFormat;
		[SerializeField] private string pullOddsFormat;
		[SerializeField] private string pullBatchFormat;
		[SerializeField] private string pullBatchFeedbackFormat;
		[SerializeField] private string pityCounterFormat;
		[SerializeField] private string pickupFormat;
		[SerializeField] private string pickupNoneText;
		[SerializeField] private string freeBoxReadyFormat;
		[SerializeField] private string freeBoxWaitFormat;
		[SerializeField] private string freeBoxFeedbackFormat;
		[SerializeField] private string oddsButtonText;
		[SerializeField] private string oddsRowFormat;
		[SerializeField] private string oddsPityFormat;
		[SerializeField] private string oddsPickupFormat;
		[SerializeField] private string oddsBatchFormat;
		[SerializeField] private string prestigeSummaryFormat;
		[SerializeField] private string prestigeAvailableFormat;
		[SerializeField] private string prestigeLockedFormat;
		[SerializeField] private string producerSummaryFormat;
		[SerializeField] private string producerRowFormat;
		[SerializeField] private string mapStageFormat;
		[SerializeField] private string mapCurrentSuffix;
		[SerializeField] private string mapBestSuffix;
		[SerializeField] private string shopScenePlaceholder;
		[SerializeField] private string labScenePlaceholder;
		[SerializeField] private string volleyTargetFeedback;
		[SerializeField] private string volleyDragHint;
		[SerializeField] private string volleyResolvedFeedback;
		[SerializeField] private string supplyFeedbackFormat;
		[SerializeField] private string appraiseCardFeedbackFormat;
		[SerializeField] private string appraiseReplacedSuffix;
		[SerializeField] private string appraiseCardEmptyFeedback;
		[SerializeField] private string nextStageFeedback;
		[SerializeField] private string statRaisedFeedbackFormat;
		[SerializeField] private string mergeFeedbackFormat;
		[SerializeField] private string mergeAllFeedbackFormat;
		[SerializeField] private string appraiseFeedbackFormat;
		[SerializeField] private string pullFeedbackFormat;
		[SerializeField] private string newHeroSuffix;
		[SerializeField] private string pitySuffix;
		[SerializeField] private string partyFullFeedback;
		[SerializeField] private string prestigeFeedback;
		[SerializeField] private string statSelectHeroTip;
		[SerializeField] private string statMaxTipFormat;
		[SerializeField] private string statWaitTipFormat;
		[SerializeField] private string statTipFormat;
		[SerializeField] private string bagTipFormat;
		[SerializeField] private string noWornGearText;
		[SerializeField] private string wornGearSummaryFormat;
		[SerializeField] private string wornEmptyTipFormat;
		[SerializeField] private string wornTipFormat;
		[SerializeField] private string secondsSpanFormat;
		[SerializeField] private string minutesSpanFormat;
		[SerializeField] private string hoursSpanFormat;
		[SerializeField] private string operationCodeFormat;
		[SerializeField] private string costFormat;
		[SerializeField] private string bossHealthFormat;
		[SerializeField] private string waveFormat;
		[SerializeField] private string partySeatFormat;
		[SerializeField] private string emptyPartySeatFormat;
		[SerializeField] private string levelFormat;
		[SerializeField] private string upgradeButtonFormat;
		[SerializeField] private string gainFormat;
		[SerializeField] private string heroChoiceFormat;
		[SerializeField] private string starPrefix;
		[SerializeField] private char starCharacter;
		[SerializeField] private string tabButtonFormat;
		[SerializeField] private string attackSpeedValueFormat;
		[SerializeField] private string percentValueFormat;
		[SerializeField] private string criticalDamageValueFormat;
		[SerializeField] private string popupPageFormat;
		[SerializeField] private int[] statUpgradeAmounts = Array.Empty<int>();
		[SerializeField, Min(1)] private int heroPopupSlotCount = 24;
		[SerializeField, Min(1)] private int gearPopupSlotCount = 24;
		[SerializeField, Min(1)] private int bagSlotCount = 40;
		[SerializeField, Min(1)] private int forgeInputSlotCount = 9;
		[SerializeField, Range(0.1f, 0.9f)] private float battleWidthShare = 0.625f;

		public int TabCount => tabs.Length;
		public int StatCount => statNames.Length;
		public int GearSlotCount => gearSlotNames.Length;
		public int StatUpgradeAmountCount => statUpgradeAmounts.Length;
		public int HeroPopupSlotCount => heroPopupSlotCount;
		public int GearPopupSlotCount => gearPopupSlotCount;
		public int BagSlotCount => bagSlotCount;
		public int ForgeInputSlotCount => forgeInputSlotCount;
		public float BattleWidthShare => battleWidthShare;

		public string TabName(int index) => tabs[index].Name;
		public string TabCaption(int index) => tabs[index].Caption;
		public bool IsTabVisible(int index) => tabs[index].Visible;
		public string GearSlotName(int index) => gearSlotNames[index];
		public string StatName(int index) => statNames[index];
		public string AxisName(IdleHeroAxis axis) => heroAxisNames[(int)axis];
		public string GradeName(IdleHeroGrade grade) => heroGradeNames[(int)grade];
		public string CardName(IdleCardKind kind) => cardNames[(int)kind];
		public string DungeonName(IdleDungeonKind kind) => dungeonNames[(int)kind];
		public int StatUpgradeAmount(int index) => statUpgradeAmounts[index];
		public int IndexOfStatUpgradeAmount(int amount) => Array.IndexOf(statUpgradeAmounts, amount);
		public string BattleGradeText(int tier, int ceiling) => string.Format(battleGradeFormat, tier, ceiling);
		public string StageText(int stage) => string.Format(stageFormat, stage);
		public string RepeatText(bool enabled) => enabled ? repeatOnText : repeatOffText;
		public string GoldAmountText(string amount) => string.Format(goldAmountFormat, amount);
		public string GoldIncomeText(string income) => string.Format(goldIncomeFormat, income);
		public string SeatText(bool main) => main ? mainSeatText : supportSeatText;
		public string GrowthTitle(string heroName) => heroName + growthSuffix;
		public string EmptySeatText => emptySeatText;
		public string MaxedText => maxedText;
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
		public string AwaySpanText(double seconds) => string.Format(awaySpanFormat, DescribeSpan(seconds));
		public string AwayWarningText(double capSeconds, double lostSeconds) =>
			string.Format(awayWarningFormat, DescribeSpan(capSeconds), DescribeSpan(lostSeconds));
		public string SelectHeroBeforeGearText => selectHeroBeforeGearText;
		public string AppraiseRowText(int tier, string count, string cost, bool available) => available
			? string.Format(appraiseAvailableFormat, tier, count, cost)
			: string.Format(appraiseUnavailableFormat, tier, count);
		public string CodexSummaryText(int score, double multiplier, int owned, int total) =>
			string.Format(codexSummaryFormat, score, multiplier, owned, total);
		public string CodexHeroText(string name, string stars, string grade, string axis) =>
			string.Format(codexOwnedHeroFormat, name, stars, grade, axis);
		public string CodexHiddenHeroText(string grade) => string.Format(codexHiddenHeroFormat, grade);
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

		public bool TryValidate(int tabCount, out string error)
		{
			if (tabs.Length != tabCount)
			{
				error = "tabs must contain " + tabCount + " entries";
				return false;
			}

			if (gearSlotNames.Length != IdleGear.SLOT_COUNT)
			{
				error = "gearSlotNames must contain " + IdleGear.SLOT_COUNT + " entries";
				return false;
			}

			int upgradeKindCount = Enum.GetValues(typeof(IdleUpgradeKind)).Length;
			if (statNames.Length != upgradeKindCount)
			{
				error = "statNames must contain " + upgradeKindCount + " entries";
				return false;
			}

			if (heroAxisNames.Length != Enum.GetValues(typeof(IdleHeroAxis)).Length)
			{
				error = "heroAxisNames does not match IdleHeroAxis";
				return false;
			}

			if (heroGradeNames.Length != Enum.GetValues(typeof(IdleHeroGrade)).Length)
			{
				error = "heroGradeNames does not match IdleHeroGrade";
				return false;
			}

			if (cardNames.Length != Enum.GetValues(typeof(IdleCardKind)).Length)
			{
				error = "cardNames does not match IdleCardKind";
				return false;
			}

			if (dungeonNames.Length != Enum.GetValues(typeof(IdleDungeonKind)).Length)
			{
				error = "dungeonNames does not match IdleDungeonKind";
				return false;
			}

			string[] requiredText =
			{
				prestigeAdviceFormat, buyProducerAdvice, raiseAdvice, mergeAdvice, wearAdvice, pullAdvice,
				seatAdvice, bagFullAdvice, tapAdvice, waitAdviceFormat, waitForCostAdvice, battleGradeFormat,
				stageFormat, repeatOnText, repeatOffText, goldAmountFormat, goldIncomeFormat, mainSeatText,
				supportSeatText, growthSuffix, emptySeatText, maxedText, bagSummaryFormat, bagFullSuffix,
				unidentifiedText, bulkMergeFormat, forgeKindFormat, forgeCellFormat, forgeResultFormat,
				forgeSelectionFormat, forgeEmptyHintFormat, salvageTitleFormat, salvageCountFormat, salvageAllText,
				salvageFeedbackFormat, lockedTipSuffix, noEquippedGearText, rawEquippedGearText,
				appraisedEquippedGearText, potentialFormat, gearPotentialFormat, dungeonRowFormat, awaySpanFormat,
				awayWarningFormat, selectHeroBeforeGearText, appraiseUnavailableFormat, appraiseAvailableFormat,
				codexSummaryFormat, codexOwnedHeroFormat, codexHiddenHeroFormat, bagUpgradeFormat, bagUpgradeMaxText,
				bagResetNoteFormat, pullAvailableFormat, pullNoStoneFormat, pullNoGoldFormat, pullOddsFormat,
				pullBatchFormat, pullBatchFeedbackFormat, pityCounterFormat, pickupFormat, pickupNoneText,
				freeBoxReadyFormat, freeBoxWaitFormat, freeBoxFeedbackFormat, oddsButtonText, oddsRowFormat,
				oddsPityFormat, oddsPickupFormat, oddsBatchFormat,
				prestigeSummaryFormat, prestigeAvailableFormat, prestigeLockedFormat, producerSummaryFormat,
				producerRowFormat, mapStageFormat, mapCurrentSuffix, mapBestSuffix, shopScenePlaceholder,
				labScenePlaceholder, volleyTargetFeedback, volleyDragHint, volleyResolvedFeedback,
				supplyFeedbackFormat, appraiseCardFeedbackFormat, appraiseReplacedSuffix,
				appraiseCardEmptyFeedback, nextStageFeedback, statRaisedFeedbackFormat, mergeFeedbackFormat,
				mergeAllFeedbackFormat, appraiseFeedbackFormat, pullFeedbackFormat, newHeroSuffix, pitySuffix,
				partyFullFeedback, prestigeFeedback, statSelectHeroTip, statMaxTipFormat, statWaitTipFormat,
				statTipFormat, bagTipFormat, noWornGearText, wornGearSummaryFormat, wornEmptyTipFormat,
				wornTipFormat, secondsSpanFormat, minutesSpanFormat, hoursSpanFormat, operationCodeFormat,
				costFormat, bossHealthFormat, waveFormat, partySeatFormat, emptyPartySeatFormat, levelFormat,
				upgradeButtonFormat, gainFormat, heroChoiceFormat, starPrefix, tabButtonFormat,
				attackSpeedValueFormat, percentValueFormat, criticalDamageValueFormat, popupPageFormat,
			};
			if (Array.Exists(requiredText, string.IsNullOrEmpty))
			{
				error = "UI text entries must not be empty";
				return false;
			}

			if (starCharacter == '\0')
			{
				error = "starCharacter must not be empty";
				return false;
			}

			if (statUpgradeAmounts.Length == 0 || Array.Exists(statUpgradeAmounts, amount => amount <= 0))
			{
				error = "statUpgradeAmounts must contain positive values";
				return false;
			}

			if (heroPopupSlotCount <= 0 || gearPopupSlotCount <= 0 || bagSlotCount <= 0 || forgeInputSlotCount <= 0
				|| battleWidthShare <= 0f || battleWidthShare >= 1f)
			{
				error = "popup count and battle width share must be in range";
				return false;
			}

			error = string.Empty;
			return true;
		}
	}
}
