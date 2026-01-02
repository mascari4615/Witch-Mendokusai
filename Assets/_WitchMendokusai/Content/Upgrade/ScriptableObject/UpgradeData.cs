using System;
using UnityEngine;

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
		Revive
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
		[field: SerializeField] public int MaxLevel { get; private set; } = 10;
		[field: SerializeField] public int[] PricePerLevel { get; private set; } = new int[1];
		[field: SerializeField] public float[] ValuePerLevel { get; private set; } = new float[1];

		[field: NonSerialized] public int CurLevel { get; set; }

		public bool TryUpgrade(out UpgradeFailReason reason, out int upgradePrice)
		{
			reason = UpgradeFailReason.None;
			upgradePrice = 0;

			if (CurLevel >= MaxLevel)
			{
				reason = UpgradeFailReason.MaxLevel;
				return false;
			}

			upgradePrice = PricePerLevel[CurLevel];
			if (upgradePrice > DataManager.Instance.GameStat[GameStatType.NYANG])
			{
				reason = UpgradeFailReason.InsufficientNyang;
				return false;
			}

			DataManager.Instance.GameStat[GameStatType.NYANG] -= upgradePrice;
			CurLevel++;
			return true;
		}

		public bool TryDowngrade(out DowngradeFailReason reason, out int refundedNyang)
		{
			reason = DowngradeFailReason.None;

			if (CurLevel <= 0)
			{
				reason = DowngradeFailReason.MinLevel;
				refundedNyang = 0;
				return false;
			}

			refundedNyang = PricePerLevel[CurLevel];
			DataManager.Instance.GameStat[GameStatType.NYANG] += refundedNyang;
			CurLevel--;
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