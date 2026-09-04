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
		[SerializeField] private int[] statUpgradeAmounts = Array.Empty<int>();
		[SerializeField, Min(1)] private int gearPopupSlotCount = 24;
		[SerializeField, Range(0.1f, 0.9f)] private float battleWidthShare = 0.625f;

		public int TabCount => tabs.Length;
		public int StatCount => statNames.Length;
		public int GearSlotCount => gearSlotNames.Length;
		public int StatUpgradeAmountCount => statUpgradeAmounts.Length;
		public int GearPopupSlotCount => gearPopupSlotCount;
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
				case IdleUpgradeKind.AttackSpeed: return string.Format("{0:0.##}/초", value);
				case IdleUpgradeKind.Defense:
				case IdleUpgradeKind.CriticalChance:
				case IdleUpgradeKind.Recovery: return string.Format("{0:P1}", value);
				case IdleUpgradeKind.CriticalDamage: return string.Format("×{0:0.00}", value);
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

			if (statUpgradeAmounts.Length == 0 || Array.Exists(statUpgradeAmounts, amount => amount <= 0))
			{
				error = "statUpgradeAmounts must contain positive values";
				return false;
			}

			if (gearPopupSlotCount <= 0 || battleWidthShare <= 0f || battleWidthShare >= 1f)
			{
				error = "popup count and battle width share must be in range";
				return false;
			}

			error = string.Empty;
			return true;
		}
	}
}
