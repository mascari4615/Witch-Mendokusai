using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 건물이 레벨업할 때 고르는 것. 종류마다 성격이 갈리게 셋만 둔다(고민이 되는 최소 수). </summary>
	public enum TowerDefenseBuildingPerk
	{
		Damage = 0, // 피해 — 정면으로 세진다.
		Range = 1,  // 사거리 — 더 넓게 관여한다(경험치도 더 는다).
		Speed = 2,  // 연사 — 자주 쏜다.
		Yield = 3,  // 산출 — 채집이 더 캔다.
		Endure = 4, // 내구 — 더 버틴다.
	}

	/// <summary>
	/// 건물 한 채의 성장(TASK-WM-194) — 경험치를 먹고 레벨이 오르면 *고를 것*이 생긴다.
	///
	/// ★ 왜 필요한가 (사용자 지시): 실시간으로 바뀌면서 「웨이브를 넘겼다」는 매듭이 사라졌다. 그 자리를
	///   메울 성장 축이 필요하다 — 게다가 판 전체가 아니라 **건물 하나하나가** 자라면 「이 포탑은 오래
	///   버틴 아이」라는 서사가 생긴다.
	/// ★ 왜 레벨업을 화면에 안 띄우나 (사용자 지시: "레벨 업 할때마다 화면에 바로 띄우면 안될 것 같고,
	///   건물 선택하면 그때 띄우거나"): 건물이 수십 채면 알림이 화면을 덮는다. 고를 것이 *쌓여 있다가*
	///   그 건물을 고를 때 나오면, 플레이어가 자기 리듬으로 처리한다.
	///
	/// 순수 — 씬·RNG·시간 0.
	/// </summary>
	public sealed class TowerDefenseBuildingProgress
	{
		private readonly int baseCost;
		private readonly float growth;

		public int Level { get; private set; } = 1;
		public int Experience { get; private set; }

		/// <summary> 아직 안 고른 선택지 수 — 레벨업할 때마다 하나씩 쌓인다. </summary>
		public int PendingChoices { get; private set; }

		/// <summary> 지금까지 고른 것들 — 같은 것을 여러 번 고를 수 있다(한 축을 밀어붙이는 것도 선택이다). </summary>
		public IReadOnlyList<TowerDefenseBuildingPerk> Taken => taken;
		private readonly List<TowerDefenseBuildingPerk> taken = new();

		/// <param name="baseCost">2레벨에 필요한 경험치.</param>
		/// <param name="growth">레벨마다 필요량이 곱해지는 비율(1.6 = 60%씩 비싸짐).</param>
		public TowerDefenseBuildingProgress(int baseCost = 10, float growth = 1.6f)
		{
			this.baseCost = Mathf.Max(1, baseCost);
			this.growth = Mathf.Max(1f, growth);
		}

		/// <summary> 다음 레벨까지 필요한 경험치(총량 아님 — 이번 레벨 구간의 크기). </summary>
		public int NextLevelCost => Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(growth, Level - 1)));

		/// <summary> 이번 구간에서 얼마나 찼나(0~1) — 화면의 바가 그린다. </summary>
		public float LevelRatio => Mathf.Clamp01(Experience / (float)NextLevelCost);

		/// <summary> 경험치를 준다. 한 번에 여러 레벨이 오를 수도 있다(큰 보상을 잘게 쪼개지 않는다). </summary>
		public void AddExperience(int amount)
		{
			if (amount <= 0)
				return;

			Experience += amount;
			while (Experience >= NextLevelCost)
			{
				Experience -= NextLevelCost;
				Level++;
				PendingChoices++;
			}
		}

		/// <summary> 선택지를 하나 고른다 — 고를 것이 없으면 아무 일도 안 일어난다. </summary>
		public bool Choose(TowerDefenseBuildingPerk perk)
		{
			if (PendingChoices <= 0)
				return false;

			PendingChoices--;
			taken.Add(perk);
			return true;
		}

		/// <summary> 그 종류를 몇 번 골랐나 — 실제 수치 보정은 이 수를 곱해서 쓴다. </summary>
		public int CountOf(TowerDefenseBuildingPerk perk)
		{
			int count = 0;
			foreach (TowerDefenseBuildingPerk item in taken)
			{
				if (item == perk)
					count++;
			}
			return count;
		}

		/// <summary>
		/// 지금 내놓을 선택지 — 건물 종류에 맞는 것 중에서 결정적으로 셋(또는 있는 만큼).
		/// 같은 건물의 같은 레벨이면 언제 열어도 같은 세 장이다(다시 열어 굴리는 짓을 막는다).
		/// </summary>
		public static void Offer(int buildingId, int level, bool isHarvester, List<TowerDefenseBuildingPerk> result)
		{
			result.Clear();

			TowerDefenseBuildingPerk[] pool = isHarvester
				? new[] { TowerDefenseBuildingPerk.Yield, TowerDefenseBuildingPerk.Range, TowerDefenseBuildingPerk.Endure }
				: new[] { TowerDefenseBuildingPerk.Damage, TowerDefenseBuildingPerk.Range, TowerDefenseBuildingPerk.Speed, TowerDefenseBuildingPerk.Endure };

			// 결정적 셔플 후 앞에서 셋.
			int[] order = new int[pool.Length];
			for (int index = 0; index < pool.Length; index++)
				order[index] = index;

			for (int index = pool.Length - 1; index > 0; index--)
			{
				int swap = Hash(buildingId, level * 31 + index) % (index + 1);
				(order[index], order[swap]) = (order[swap], order[index]);
			}

			int take = Mathf.Min(3, pool.Length);
			for (int index = 0; index < take; index++)
				result.Add(pool[order[index]]);
		}

		/// <summary> 화면에 그대로 쓰는 이름. </summary>
		public static string NameOf(TowerDefenseBuildingPerk perk)
		{
			return perk switch
			{
				TowerDefenseBuildingPerk.Damage => "피해 강화",
				TowerDefenseBuildingPerk.Range => "사거리 강화",
				TowerDefenseBuildingPerk.Speed => "연사 강화",
				TowerDefenseBuildingPerk.Yield => "산출 강화",
				_ => "내구 강화",
			};
		}

		private static int Hash(int a, int b)
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 486187739 + a;
				hash = hash * 486187739 + b;
				hash ^= hash >> 15;
				return hash & 0x7fffffff;
			}
		}
	}
}
