using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum BreakableDropType
	{
		Nyang = 0,
		Heal = 1,
		Magnet = 2,
		Freeze = 3,
		ClearScreen = 4,
		Exp = 5,
	}

	[Serializable]
	public struct BreakableDrop
	{
		public BreakableDropType Type;
		[Min(0f), Tooltip("종류별 가중치. 전체 합 대비 비율로 고름")] public float Weight;
		[Tooltip("떨어뜨릴 개수 범위, 양 끝 포함")] public Vector2Int Count;
	}

	// 부서지는 물건의 떨어뜨림 표
	[CreateAssetMenu(fileName = nameof(BreakableTable), menuName = "WM/Breakable/" + nameof(BreakableTable))]
	public class BreakableTable : ScriptableObject
	{
		[Range(0f, 1f), Tooltip("부쉈을 때 무엇이든 떨어질 확률")] public float DropChance = .85f;
		[Min(1), Tooltip("냥 동전 하나의 값")] public int NyangPerCoin = 2;
		public BreakableDrop[] Drops = Array.Empty<BreakableDrop>();

		public float TotalWeight
		{
			get
			{
				float total = 0f;
				foreach (BreakableDrop drop in Drops)
					total += drop.Weight;
				return total;
			}
		}

		// 확률 굴림과 가중치 굴림, 둘 다 0 이상 1 미만. 빈손이면 -1
		public int Pick(float chanceRoll, float weightRoll)
		{
			if (chanceRoll >= DropChance)
				return -1;
			float total = TotalWeight;
			if (total <= 0f)
				throw new InvalidOperationException($"{name}: {nameof(Drops)} 가중치 합 0");
			float remaining = weightRoll * total;
			for (int i = 0; i < Drops.Length; i++)
			{
				remaining -= Drops[i].Weight;
				if (remaining < 0f)
					return i;
			}
			return Drops.Length - 1;
		}
	}
}
