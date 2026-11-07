using System;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public enum UpgradeType
	{
		AttackPower,
		HPMax,
		HPRegen,
		CooldownReduction,
		AttackCount,
		AttackRange,
		Knockback,
		CriticalChance,
		MoveSpeed,
		PickupRange,
		Luck,
		ExpBonus,
		GoldBonus,
		InvincibilityTime,
		Evasion,
		Revive,
		RerollCharge = 16,
		SkipCharge = 17,
		BanCharge = 18
	}

	public enum UpgradeFailReason
	{
		None,
		InsufficientNyang,
		MaxLevel
	}

	public enum DowngradeFailReason
	{
		None,
		MinLevel
	}

	public struct UpgradeSaveData
	{
		public int Level;
	}

	[CreateAssetMenu(fileName = nameof(UpgradeData), menuName = "WM/Variable/UpgradeData")]
	public class UpgradeData : DataSO, ISavable<UpgradeSaveData>
	{
		[field: Header("_" + nameof(UpgradeData))]
		[field: SerializeField] public UpgradeType Type { get; private set; } = new();
		[field: SerializeField] public UnitStatType UnitStatType { get; private set; } = new();
		[field: SerializeField] public int MaxLevel { get; private set; } = 10;
		[field: SerializeField] public int[] PricePerLevel { get; private set; } = new int[1];
		[field: SerializeField] public float[] ValuePerLevel { get; private set; } = new float[1];
		[field: SerializeField] public string DisplayName { get; private set; } = "";
		[field: SerializeField] public string DisplayDescription { get; private set; } = "";
		[field: SerializeField] public UpgradeCategory Category { get; private set; } = UpgradeCategory.Attack;
		[field: SerializeField] public int DisplayOrder { get; private set; } = 0;
		[field: SerializeField] public bool EffectPending { get; private set; } = false;

		[field: NonSerialized] public int CurLevel { get; set; }

		// 스탯 칸 없는 행운의 배율, 희귀 카드 가중치와 회복, 자석 획득 확률에 곱함
		public static float LuckMultiplier => 1f + (float)Get<UpgradeData>((int)UpgradeType.Luck).TotalValue / UnitStat.PERCENT;

		public static int BonusOf(UpgradeType type) => Get<UpgradeData>((int)type).TotalValue;

		public int TotalValue
		{
			get
			{
				int total = 0;
				for (int i = 0; i < CurLevel; i++)
				{
					total += (int)ValuePerLevel[i];
				}
				return total;
			}
		}

		// TASK-WM-107 Slice 3-3 — static Effect.ApplyEffect 우회 폐기. UpgradeData 는 순수 데이터,
		// dispatch 는 DI caller(GameManager.effectRunner) 책임 (POCO 가 매니저/static 안 앎 = seed 정수).
		public EffectInfo ToEffectInfo() => new EffectInfo
		{
			Type = EffectType.UnitStat,
			Data = Get<UnitStatData>((int)UnitStatType),
			ArithmeticOperator = ArithmeticOperator.Add,
			Value = TotalValue
		};

		public bool TryUpgrade(GameStat gameStat, out UpgradeFailReason reason, out int upgradePrice)
		{
			reason = UpgradeFailReason.None;
			upgradePrice = 0;

			if (CurLevel >= MaxLevel)
			{
				reason = UpgradeFailReason.MaxLevel;
				return false;
			}

			upgradePrice = PricePerLevel[CurLevel];
			if (upgradePrice > gameStat[GameStatType.NYANG])
			{
				reason = UpgradeFailReason.InsufficientNyang;
				return false;
			}

			gameStat[GameStatType.NYANG] -= upgradePrice;
			CurLevel++;
			return true;
		}

		public bool TryDowngrade(GameStat gameStat, out DowngradeFailReason reason, out int refundedNyang)
		{
			reason = DowngradeFailReason.None;

			if (CurLevel <= 0)
			{
				reason = DowngradeFailReason.MinLevel;
				refundedNyang = 0;
				return false;
			}

			CurLevel--;
			refundedNyang = PricePerLevel[CurLevel];
			gameStat[GameStatType.NYANG] += refundedNyang;
			return true;
		}

		public bool TryReset(GameStat gameStat, out DowngradeFailReason reason, out int refundedNyang)
		{
			reason = DowngradeFailReason.None;

			if (CurLevel <= 0)
			{
				reason = DowngradeFailReason.MinLevel;
				refundedNyang = 0;
				return false;
			}

			refundedNyang = PricePerLevel.Take(CurLevel).Sum();
			gameStat[GameStatType.NYANG] += refundedNyang;
			CurLevel = 0;
			return true;
		}

		public void Load(UpgradeSaveData saveData)
		{
			CurLevel = saveData.Level;
		}

		public UpgradeSaveData Save()
		{
			return new UpgradeSaveData()
			{
				Level = CurLevel
			};
		}
	}
}