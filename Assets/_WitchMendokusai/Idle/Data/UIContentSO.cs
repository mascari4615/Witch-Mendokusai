using System;
using UnityEngine;
using UnityEngine.Serialization;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleUIContent", menuName = "WM/Idle/UI Content")]
	public sealed partial class UIContentSO : ScriptableObject
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
		[SerializeField] private string dungeonTicketFormat;
		[SerializeField] private string dungeonRefillFormat;
		[SerializeField] private string dungeonEnterText;
		[SerializeField] private string dungeonSweepFormat;
		[SerializeField] private string dungeonClosedText;
		[SerializeField] private string dungeonGoldRewardFormat;
		[SerializeField] private string dungeonBossRewardFormat;
		[SerializeField] private string dungeonGearRewardFormat;
		[SerializeField] private string dungeonFeedbackFormat;
		[SerializeField] private string awaySpanFormat;
		[SerializeField] private string awayWarningFormat;
		[SerializeField] private string selectHeroBeforeGearText;
		[SerializeField] private string appraiseUnavailableFormat;
		[SerializeField] private string appraiseAvailableFormat;
		[FormerlySerializedAs("discoverySummaryFormat")]
		[SerializeField] private string discoverySummaryFormat;
		[FormerlySerializedAs("discoveryOwnedHeroFormat")]
		[SerializeField] private string discoveryOwnedHeroFormat;
		[FormerlySerializedAs("discoveryHiddenHeroFormat")]
		[SerializeField] private string discoveryHiddenHeroFormat;
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
		[SerializeField] private string volleyMissFeedback;
		[SerializeField] private string volleyTapHint;
		[SerializeField] private string gachaTitleFormat;
		[SerializeField] private string gachaSummaryFormat;
		[SerializeField] private string gachaSkipText;
		[SerializeField] private string gachaCloseText;
		[SerializeField] private string gachaNewBadge;
		[SerializeField] private string gachaPityBadge;
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
				dungeonTicketFormat, dungeonRefillFormat, dungeonEnterText, dungeonSweepFormat, dungeonClosedText,
				dungeonGoldRewardFormat, dungeonBossRewardFormat, dungeonGearRewardFormat, dungeonFeedbackFormat,
				awayWarningFormat, selectHeroBeforeGearText, appraiseUnavailableFormat, appraiseAvailableFormat,
				discoverySummaryFormat, discoveryOwnedHeroFormat, discoveryHiddenHeroFormat, bagUpgradeFormat, bagUpgradeMaxText,
				bagResetNoteFormat, pullAvailableFormat, pullNoStoneFormat, pullNoGoldFormat, pullOddsFormat,
				pullBatchFormat, pullBatchFeedbackFormat, pityCounterFormat, pickupFormat, pickupNoneText,
				freeBoxReadyFormat, freeBoxWaitFormat, freeBoxFeedbackFormat, oddsButtonText, oddsRowFormat,
				oddsPityFormat, oddsPickupFormat, oddsBatchFormat,
				prestigeSummaryFormat, prestigeAvailableFormat, prestigeLockedFormat, producerSummaryFormat,
				producerRowFormat, mapStageFormat, mapCurrentSuffix, mapBestSuffix, shopScenePlaceholder,
				labScenePlaceholder, volleyTargetFeedback, volleyDragHint, volleyResolvedFeedback,
				supplyFeedbackFormat, appraiseCardFeedbackFormat, appraiseReplacedSuffix,
				volleyMissFeedback, volleyTapHint, gachaTitleFormat, gachaSummaryFormat, gachaSkipText,
				gachaCloseText, gachaNewBadge, gachaPityBadge, appraiseCardEmptyFeedback, nextStageFeedback, statRaisedFeedbackFormat, mergeFeedbackFormat,
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

