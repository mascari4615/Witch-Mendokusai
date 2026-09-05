using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 웨이브 사이 드래프트(TASK-WM-194) — 웨이브를 넘길 때마다 **3장 중 1장을 반드시 고른다**.
	///
	/// ★ 왜 필요한가: 지금 경제는 「자원이 쌓이면 살 수 있는 걸 산다」라, 고민이 아니라 *대기*다.
	///   드래프트는 자원과 무관하게 **매 웨이브 강제 선택**을 만든다 — 포기한 두 장이 곧 이번 판의 성격이 된다.
	///   판 밖 뽑기(유물)와 층이 다르다: 저건 다음 판에 남고, 이건 이 판에서만 산다.
	///
	/// ★ 왜 결정적인가: 같은 판(씨앗)의 같은 웨이브면 같은 세 장이 나온다. 예고가 거짓말을 못 하는 것과 같은 이유로,
	///   「다시 뽑기」로 흔들 수 있으면 선택의 무게가 사라진다.
	///
	/// 순수 정적 — 씬·RNG·시간 0. EditMode 로 전량 검증.
	/// </summary>
	public static class TowerDefenseDraft
	{
		/// <summary> 이 판에서 뽑을 수 있는 것 전부. 새 종류를 늘리는 자리는 여기 하나다. </summary>
		private static readonly TowerDefenseBoonKind[] Pool =
		{
			TowerDefenseBoonKind.Firepower,
			TowerDefenseBoonKind.Income,
			TowerDefenseBoonKind.Bounty,
			TowerDefenseBoonKind.Life,
			TowerDefenseBoonKind.Essence,
			TowerDefenseBoonKind.Windfall,
			TowerDefenseBoonKind.EssenceRate,
			TowerDefenseBoonKind.PowerCapacity,
			TowerDefenseBoonKind.SupplyReach,
			TowerDefenseBoonKind.Vision,
			TowerDefenseBoonKind.BuildDiscount,
			TowerDefenseBoonKind.Experience,
			TowerDefenseBoonKind.ResearchDiscount,
			TowerDefenseBoonKind.EnemySlow,
			TowerDefenseBoonKind.MaxLives,
			TowerDefenseBoonKind.HarvestYield,
			TowerDefenseBoonKind.TrapPower,
			TowerDefenseBoonKind.CoreRepair,
			TowerDefenseBoonKind.NestDamage,
			TowerDefenseBoonKind.EnemyReward,
		};

		/// <summary>
		/// waveIndex 파를 넘긴 직후 내놓을 카드들. 서로 다른 종류만 나온다(같은 걸 두 장 주면 선택이 아니다).
		/// count 가 종류 수보다 크면 종류 수만큼만.
		/// </summary>
		public static void Offer(int waveIndex, int seed, TowerDefenseDraftRules rules, List<TowerDefenseBoon> result)
		{
			result.Clear();

			int offerCount = rules.OfferCount;
			if (offerCount <= 0)
				return;
			if (offerCount > Pool.Length)
				offerCount = Pool.Length;

			// 결정적 셔플 — 같은 (씨앗, 웨이브) 면 같은 순서. 앞에서 offerCount 장을 뗀다.
			Span<int> order = stackalloc int[Pool.Length];
			for (int index = 0; index < Pool.Length; index++)
				order[index] = index;

			for (int index = Pool.Length - 1; index > 0; index--)
			{
				int swap = Hash(seed, waveIndex * 31 + index) % (index + 1);
				(order[index], order[swap]) = (order[swap], order[index]);
			}

			for (int index = 0; index < offerCount; index++)
				result.Add(Make(Pool[order[index]], rules));
		}

		/// <summary> 한 종류의 카드 한 장 — 이름·설명·크기가 전부 여기서 정해진다(화면은 이 값을 그대로 읽는다). </summary>
		public static TowerDefenseBoon Make(TowerDefenseBoonKind kind, TowerDefenseDraftRules rules)
		{
			switch (kind)
			{
				case TowerDefenseBoonKind.Firepower:
					return new TowerDefenseBoon(kind, rules.FirepowerBonus,
						"벼려진 손끝", "모든 인형의 피해 +" + Percent(rules.FirepowerBonus));

				case TowerDefenseBoonKind.Income:
					return new TowerDefenseBoon(kind, rules.IncomeBonus,
						"넉넉한 살림", "웨이브 정산 +" + Percent(rules.IncomeBonus));

				case TowerDefenseBoonKind.Bounty:
					return new TowerDefenseBoon(kind, rules.BountyBonus,
						"사냥의 값", "마수 격파 보상 +" + Percent(rules.BountyBonus));

				case TowerDefenseBoonKind.Life:
					return new TowerDefenseBoon(kind, rules.LivesBonus,
						"한 번 더 버틴다", "목숨 +" + (int)rules.LivesBonus);

				case TowerDefenseBoonKind.Essence:
					return new TowerDefenseBoon(kind, rules.EssenceBonus,
						"응결된 정수", "정수 +" + (int)rules.EssenceBonus);

				case TowerDefenseBoonKind.EssenceRate:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"정수의 결", "정수 정산 +" + Percent(rules.RateBonus));

				case TowerDefenseBoonKind.PowerCapacity:
					return new TowerDefenseBoon(kind, rules.PowerBonus,
						"여분의 불씨", "코어 전기 +" + (int)rules.PowerBonus);

				case TowerDefenseBoonKind.SupplyReach:
					return new TowerDefenseBoon(kind, rules.ReachBonus,
						"길게 뻗은 손", "보급·건설 거리 +" + Percent(rules.ReachBonus));

				case TowerDefenseBoonKind.Vision:
					return new TowerDefenseBoon(kind, rules.ReachBonus,
						"멀리 보는 눈", "모든 시야 +" + Percent(rules.ReachBonus));

				case TowerDefenseBoonKind.BuildDiscount:
					return new TowerDefenseBoon(kind, rules.DiscountBonus,
						"손에 익은 일", "건설 비용 -" + Percent(rules.DiscountBonus));

				case TowerDefenseBoonKind.Experience:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"빨리 배운다", "건물 경험치 +" + Percent(rules.RateBonus));

				case TowerDefenseBoonKind.ResearchDiscount:
					return new TowerDefenseBoon(kind, rules.DiscountBonus,
						"앞선 설계", "연구 비용 -" + Percent(rules.DiscountBonus));

				case TowerDefenseBoonKind.EnemySlow:
					return new TowerDefenseBoon(kind, rules.SlowBonus,
						"무거운 걸음", "앞으로 나오는 마수가 " + Percent(rules.SlowBonus) + " 느려진다");

				case TowerDefenseBoonKind.MaxLives:
					return new TowerDefenseBoon(kind, rules.LivesBonus,
						"두꺼운 성벽", "최대 목숨 +" + (int)rules.LivesBonus + " (즉시 회복)");

				case TowerDefenseBoonKind.HarvestYield:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"굵은 광맥", "채집 산출 +" + Percent(rules.RateBonus));

				case TowerDefenseBoonKind.TrapPower:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"날 선 바닥", "함정 피해 +" + Percent(rules.RateBonus));

				case TowerDefenseBoonKind.CoreRepair:
					return new TowerDefenseBoon(kind, rules.RepairRatio,
						"코어 수복", "코어 체력 " + Percent(rules.RepairRatio) + " 회복");

				case TowerDefenseBoonKind.NestDamage:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"둥지 파쇄", "둥지에 주는 피해 +" + Percent(rules.RateBonus));

				case TowerDefenseBoonKind.EnemyReward:
					return new TowerDefenseBoon(kind, rules.RateBonus,
						"값진 사냥", "마수를 잡을 때 코어 경험치 +" + Percent(rules.RateBonus));

				default:
					return new TowerDefenseBoon(TowerDefenseBoonKind.Windfall, rules.WindfallResource,
						"뜻밖의 보급", "자원 +" + (int)rules.WindfallResource);
			}
		}

		private static string Percent(float ratio)
		{
			return (int)(ratio * 100f) + "%";
		}

		// 곱셈 해시 — 같은 입력이면 같은 값. UnityEngine.Random 을 쓰면 다른 곳의 뽑기와 순서가 얽혀 결정성이 깨진다.
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
