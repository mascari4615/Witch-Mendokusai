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

	public struct UpgradeDataSave
	{
		public UpgradeType Type;
		public int Level;
	}

	[CreateAssetMenu(fileName = nameof(UpgradeData), menuName = "WM/Variable/UpgradeData")]
	public class UpgradeData : DataSO, ISavable<UpgradeDataSave>
	{
		[field: Header("_" + nameof(UpgradeData))]
		[field: SerializeField] public UpgradeType Type { get; private set; } = new();
		[field: SerializeField] public int MaxLevel { get; private set; } = 10;
		[field: SerializeField] public int[] PricePerLevel { get; private set; } = new int[1];
		[field: SerializeField] public float[] ValuePerLevel { get; private set; } = new float[1];

		[field: NonSerialized] public int CurLevel { get; set; }

		public void Load(UpgradeDataSave saveData)
		{
			CurLevel = saveData.Level;
		}

		public UpgradeDataSave Save()
		{
			return new UpgradeDataSave()
			{
				Type = Type,
				Level = CurLevel
			};
		}
	}
}