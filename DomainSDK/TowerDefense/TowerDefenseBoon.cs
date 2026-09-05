using WitchMendokusai.Numerics;
using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 드래프트 카드 한 장의 종류(TASK-WM-194). </summary>
	public enum TowerDefenseBoonKind
	{
		Firepower = 0, // 모든 인형의 피해.
		Income = 1,    // 웨이브 정산.
		Bounty = 2,    // 격파 보상.
		Life = 3,      // 목숨(즉시).
		Essence = 4,   // 정수(즉시).
		Windfall = 5,  // 자원(즉시).

		// ── 확장(6종 → 20종) — 카드가 적으면 코어 레벨이 오를수록 같은 것이 반복된다.
		//    새 종류는 전부 *이미 있는 수치*에 걸린다 — 화면만 바뀌고 실물이 그대로면 그건 선택이 아니다.
		EssenceRate = 6,       // 정수 정산 배수.
		PowerCapacity = 7,     // 코어 전기 용량(즉시, 영구).
		SupplyReach = 8,       // 보급·건설 거리 배수.
		Vision = 9,            // 모든 시야 배수.
		BuildDiscount = 10,    // 건설 비용 할인.
		Experience = 11,       // 건물 경험치 배수.
		ResearchDiscount = 12, // 코어 연구 비용 할인.
		EnemySlow = 13,        // 앞으로 나오는 마수의 속도 감소.
		MaxLives = 14,         // 최대 목숨 증가(즉시 회복).
		HarvestYield = 15,     // 채집 산출 배수.
		TrapPower = 16,        // 함정 피해 배수.
		CoreRepair = 17,       // 코어 즉시 회복.
		NestDamage = 18,       // 둥지에 주는 피해 배수.
		EnemyReward = 19,      // 잡을 때 경험치까지 더 준다(코어 성장 가속).
	}

	/// <summary> 카드 한 장 — 화면은 이 세 값만 읽고 그린다. </summary>
	public readonly struct TowerDefenseBoon
	{
		public readonly TowerDefenseBoonKind Kind;
		public readonly float Magnitude;
		public readonly string DisplayName;
		public readonly string Note;

		public TowerDefenseBoon(TowerDefenseBoonKind kind, float magnitude, string displayName, string note)
		{
			Kind = kind;
			Magnitude = magnitude;
			DisplayName = displayName;
			Note = note;
		}

		public bool IsValid => string.IsNullOrEmpty(DisplayName) == false;
	}

	/// <summary>
	/// 드래프트 수치 노브 — 전부 스테이지 SO 로 노출(수치 노출 룰: 하드코딩 0).
	/// 즉시 효과(목숨·정수·자원)와 지속 효과(피해·정산·보상 배수)가 한 표에 있어야 세기를 나란히 볼 수 있다.
	/// </summary>
	[Serializable]
	public struct TowerDefenseDraftRules
	{
		public int OfferCount;         // 한 번에 내놓는 장수(0 이면 드래프트 없음).
		public float FirepowerBonus;   // 누적 피해 증가 비율(0.15 = 장당 +15%).
		public float IncomeBonus;      // 누적 정산 증가 비율.
		public float BountyBonus;      // 누적 격파 보상 증가 비율.
		public float LivesBonus;       // 즉시 목숨.
		public float EssenceBonus;     // 즉시 정수.
		public float WindfallResource; // 즉시 자원.

		// 확장 카드 — 값 하나가 곧 그 카드의 세기다(수치 노출 룰: 하드코딩 0).
		public float RateBonus;        // 배수형 카드 공통 증가폭(정수·경험치·산출·함정·둥지 등).
		public float DiscountBonus;    // 할인형 카드 공통 감소폭.
		public float ReachBonus;       // 거리·시야형 카드 공통 증가폭.
		public float PowerBonus;       // 전기 용량(즉시).
		public float SlowBonus;        // 마수 속도 감소폭.
		public float RepairRatio;      // 코어 회복 비율(최대 체력 대비).

		public bool IsEnabled => OfferCount > 0;
	}

	/// <summary>
	/// 이 판에서 고른 것들의 누적(TASK-WM-194). 지속 효과는 여기 쌓이고, 즉시 효과는 셸이 그 자리에서 지급한다
	/// — 「무엇이 쌓였나」와 「무엇을 받았나」를 한 곳에 섞으면 화면이 거짓말을 하게 된다.
	/// </summary>
	public sealed class TowerDefenseBoonState
	{
		private float firepower;
		private float income;
		private float bounty;
		private float essenceRate;
		private float supplyReach;
		private float vision;
		private float buildDiscount;
		private float experience;
		private float researchDiscount;
		private float enemySlow;
		private float harvestYield;
		private float trapPower;
		private float nestDamage;
		private float enemyReward;

		/// <summary> 지금까지 고른 장수 — 화면이 「N번째 선택」을 말할 때 쓴다. </summary>
		public int TakenCount { get; private set; }

		/// <summary>
		/// 지금까지 고른 카드의 종류들 — 이 상태가 *무엇 때문에 이렇게 됐는지*의 기록.
		/// ★ 왜 필요한가: 쌓인 수치만 있으면 저장이 「이 판의 성격」을 적을 수 없다. 종류만 적어두면
		///   값은 그 판의 규칙에서 다시 나오므로(같은 규칙 = 같은 값), 저장이 작고 규칙이 바뀌어도 산다.
		/// </summary>
		public IReadOnlyList<TowerDefenseBoonKind> TakenKinds => takenKinds;
		private readonly List<TowerDefenseBoonKind> takenKinds = new();

		public void Take(TowerDefenseBoon boon)
		{
			TakenCount++;
			takenKinds.Add(boon.Kind);

			switch (boon.Kind)
			{
				case TowerDefenseBoonKind.Firepower:
					firepower += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Income:
					income += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Bounty:
					bounty += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.EssenceRate:
					essenceRate += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.SupplyReach:
					supplyReach += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Vision:
					vision += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.BuildDiscount:
					buildDiscount += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Experience:
					experience += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.ResearchDiscount:
					researchDiscount += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.EnemySlow:
					enemySlow += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.HarvestYield:
					harvestYield += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.TrapPower:
					trapPower += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.NestDamage:
					nestDamage += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.EnemyReward:
					enemyReward += boon.Magnitude;
					break;
				// 즉시 효과는 상태에 안 쌓인다 — 받은 순간 끝.
				default:
					break;
			}
		}

		public float DamageMultiplier => 1f + firepower;
		public float IncomeMultiplier => 1f + income;
		public float BountyMultiplier => 1f + bounty;
		public float EssenceMultiplier => 1f + essenceRate;
		public float SupplyReachMultiplier => 1f + supplyReach;
		public float VisionMultiplier => 1f + vision;
		public float ExperienceMultiplier => 1f + experience;
		public float HarvestYieldMultiplier => 1f + harvestYield;
		public float TrapPowerMultiplier => 1f + trapPower;
		public float NestDamageMultiplier => 1f + nestDamage;
		public float EnemyRewardMultiplier => 1f + enemyReward;

		/// <summary> 비용 할인 — 절대 공짜가 되지 않게 상한을 둔다(공짜면 선택이 아니라 스위치다). </summary>
		public float CostMultiplier => Mathf.Max(0.35f, 1f - buildDiscount);
		public float ResearchCostMultiplier => Mathf.Max(0.35f, 1f - researchDiscount);

		/// <summary> 마수 속도 — 절반 아래로는 안 내려간다(멈춘 적은 적이 아니다). </summary>
		public float EnemySpeedMultiplier => Mathf.Max(0.5f, 1f - enemySlow);

		/// <summary>
		/// 지금까지 고른 것이 판에 무엇을 남겼는지 한 줄 — 화면이 이걸 그대로 읽는다.
		///
		/// ★ 예전엔 열넷 중 *셋*(피해·정산·보상)만 말했다. 나머지를 고르면 화면이 침묵해서
		///   「방금 뭘 골랐는데 아무 일도 안 일어난 것 같다」가 됐다(라이브 실측: 한 장 골랐는데 요약이 빈칸).
		///   쌓이는 것은 전부 말한다.
		/// ★ 즉시 효과(목숨·정수·자원 같은 것)는 쌓이는 수치가 없어 여기 남길 것이 없다 —
		///   그래서 *장수*를 앞에 둔다. 「N장 고름」이 있으면 화면이 최소한 거짓말은 안 한다.
		/// </summary>
		public string Describe()
		{
			if (TakenCount == 0)
				return string.Empty;

			string text = TakenCount + "장";
			text += Percent("피해", firepower);
			text += Percent("정산", income);
			text += Percent("보상", bounty);
			text += Percent("정수", essenceRate);
			text += Percent("보급", supplyReach);
			text += Percent("시야", vision);
			text += Percent("경험", experience);
			text += Percent("채집", harvestYield);
			text += Percent("함정", trapPower);
			text += Percent("둥지", nestDamage);
			text += Percent("마수보상", enemyReward);
			text += Percent("건설값↓", buildDiscount);
			text += Percent("연구값↓", researchDiscount);
			text += Percent("마수둔화", enemySlow);
			return text;
		}

		private static string Percent(string label, float ratio)
		{
			return ratio > 0f ? "  ·  " + label + " +" + (int)(ratio * 100f) + "%" : string.Empty;
		}

		public void Reset()
		{
			firepower = 0f;
			income = 0f;
			bounty = 0f;
			essenceRate = 0f;
			supplyReach = 0f;
			vision = 0f;
			buildDiscount = 0f;
			experience = 0f;
			researchDiscount = 0f;
			enemySlow = 0f;
			harvestYield = 0f;
			trapPower = 0f;
			nestDamage = 0f;
			enemyReward = 0f;
			TakenCount = 0;
			takenKinds.Clear();
		}
	}
}
