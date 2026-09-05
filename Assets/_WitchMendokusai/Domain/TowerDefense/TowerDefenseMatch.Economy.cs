using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	// TowerDefenseMatch 의 Economy 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 자원 노드 점유 — 채집건물은 반드시 미점유 노드를 잡아야 가동(개척 리스크). index = stage.ResourceNodePositions 인덱스.
		private readonly HashSet<int> claimedNodes = new();

		// 가동 중인 채집 인형 위치 — 정산 때 각자 머리 위에 벌어들인 액수를 띄운다.
		// 숫자가 *어디서* 나오는지 안 보이면 채집 인형은 그냥 서 있는 장식으로 읽힌다.
		private readonly List<Transform> harvesterTransforms = new();
		// 이 채집 인형이 바깥 노드에 섰나 — 정수/자원 중 무엇을 내는지 결정.
		// ★ 열쇠는 반드시 *그 인형 자신*이다. 좌표를 열쇠로 쓰면 유닛이 바닥에 앉으며 소수점이 미세하게
		//   달라지는 순간 조회가 통째로 빗나가 **바깥 노드가 영원히 안쪽으로 취급된다** = 정수 수입 0
		//   (사용자 실증: "전초지기랑 연구소 설치 안됨" — 정수로만 사는 것들이 통째로 잠겨 있었다).
		private readonly Dictionary<Transform, bool> harvesterIsOuter = new();
		// 보급 — 코어에서 내 건물을 징검다리로 이어지는 사슬. 끊기면 그 너머의 채집은 수입이 0.
		private readonly TowerDefenseSupplyChain supplyChain = new();
		// 전초기지 — 마수가 향하는 *또 하나의 목표*이자 보급의 새 원점.
		private readonly List<Transform> outposts = new();

		// 고른 카드가 쌓이는 곳 — 코어 레벨업 선택이 여기로 들어온다.
		// 카드가 걸려 있는 동안 진행이 멈춘다 = 「강제 선택」의 실체.
		private readonly TowerDefenseBoonState boons = new();

		// 보급 원점(코어·전초기지)의 원 — 보급 거리 연구가 오르면 이것도 같이 자라야 한다.
		private readonly List<TowerDefenseRing> supplyRings = new();

		// 이름 붙은 인형들 — 화면이 이름표를 띄우는 데 필요한 최소 정보.
		private readonly List<TowerDefenseDollLabel> dollLabels = new();

		private readonly List<Vector3> activeNodePositions = new();
		private readonly List<float> activeNodeIncomeMultipliers = new();
		// 노드 등급 — 바깥 노드는 정수를 낸다(안쪽은 자원). 「멀리 나가야 강해진다」의 근거.
		private readonly List<bool> activeNodeIsOuter = new();

		/// <summary> 전초기지 위치들 — 미니맵이 「내가 넓힌 곳」을 그린다. </summary>
		public IReadOnlyList<Transform> Outposts => outposts;

		public int Resource => core != null ? core.Resource : 0;
		public int Essence => core != null ? core.Essence : 0;
		// 수입 가중치를 보급이 정하게 되면서 core 의 누적 카운트는 늘 0 이 됐다 — 실제 목록이 진실.
		public int HarvesterCount => harvesterTransforms.Count;

		/// <summary>
		/// 이번 판의 자원 노드 위치 — **무대 로컬 좌표**다. 쓰기 전에 `StageRoot.TransformPoint` 로 옮겨야 한다.
		///
		/// ★ 이름에 로컬을 박아둔 이유: 옆의 배치 API(TryPlaceHarvester/CanBuildAt/TryFindPlaceableNode)는
		///   전부 *월드* 를 받는다. 그대로 넘기면 판이 원점에서 멀리 있을 때(개척은 z≈2000) 전부 조용히
		///   거절당한다 — 오류도 로그도 없이 「왜 안 지어지지」만 남는다(실측: 노드까지 1906칸으로 계산됨).
		/// </summary>
		public IReadOnlyList<Vector3> ActiveResourceNodeLocalPositions => activeNodePositions;

		/// <summary> index 번 노드의 벌이 배수 — 화면 표시와 실제 수입이 같은 값을 읽는다. </summary>
		public float NodeIncomeMultiplierAt(int index)
		{
			return index >= 0 && index < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[index] : 1f;
		}

		/// <summary> 지금까지 고른 것 한 줄 요약(없으면 빈 문자열). </summary>
		public string BoonSummary => boons.Describe();

		/// <summary> 지금까지 고른 장수. </summary>
		public int BoonCount => boons.TakenCount;

		// ── 이름 붙은 인형 ────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 「광역 포탑」은 물건이고, 물건은 팔 때 아깝지 않다. 이름이 붙는 순간 같은 유닛이
		//   아이가 되어 잃는 것에 무게가 생긴다. 개척은 마녀가 인형을 데리고 나가는 이야기다.

		/// <summary> 화면에 띄울 이름표들 — 사라진 앵커는 조회 겸 정리(멱등). </summary>
		public IReadOnlyList<TowerDefenseDollLabel> DollLabels
		{
			get
			{
				for (int index = dollLabels.Count - 1; index >= 0; index--)
				{
					if (dollLabels[index].IsAlive == false)
					{
						// ★ *잃은* 것만 센다. 판 것은 내가 치운 것이지 뺏긴 것이 아닌데,
						//   둘을 같이 세면 판 요약의 「잃음」이 판매 횟수만큼 부풀어 거짓말을 한다.
						if (soldDolls.Remove(dollLabels[index]) == false)
							LostCount++;
						dollLabels.RemoveAt(index);
					}
				}
				return dollLabels;
			}
		}

		/// <summary>
		/// 보급 원점(코어·전초기지)에 사거리 원 — 「사슬이 여기서 출발해 이만큼 닿는다」.
		/// 이 원이 없으면 채집을 어디에 세워야 이어지는지가 순수한 시행착오가 된다.
		/// </summary>
		private void ShowSupplyReachRing(Transform origin)
		{
			if (origin == null || stage == null || EffectiveSupplyReach <= 0f)
				return;

			Color ringColor = stage.HarvesterTint;
			ringColor.a = 0.18f;
			TowerDefenseRing ring = TowerDefenseRing.Create(origin, "SupplyReachRing", ringColor, 0.06f, 0.03f);
			ring.SetRadius(EffectiveSupplyReach);
			supplyRings.Add(ring); // 연구로 보급이 길어지면 이 원도 따라 커져야 한다.
		}


		// ── 전기 ─────────────────────────────────────────────────────────────────
		// 이 층은 통째로 떨어져 나갔다 — 매치가 4000줄이 넘어 「한 덩어리가 너무 많은 걸 아는」 병이
		// 실제 결함으로 몇 번 나왔다. 여기 남는 것은 *물어보고 넘겨주는 일*뿐이다.
		private readonly TowerDefensePowerGrid powerGrid = new();

		/// <summary> 전체 전기 용량 / 요구 — 화면이 「얼마나 모자라나」를 말한다. </summary>
		public int PowerCapacity => powerGrid.Capacity;
		public int PowerDemand => powerGrid.Demand;

		/// <summary> 전기를 못 받아 멈춘 건물 수. </summary>
		public int UnpoweredBuildings => powerGrid.UnpoweredBuildings;

		private void RefreshPower()
		{
			if (coreCombatant == null)
				return;

			powerGrid.Refresh(stage, coreCombatant.Position, bonusPowerCapacity,
				harvesterTransforms.Contains, FindDollLabel, Time.deltaTime);
		}

		/// <summary>
		/// 발전 인형 배치 — 자원으로 짓고, 범위 안 건물에 전기를 댄다.
		/// 보급 사슬의 징검다리도 겸한다(내 건물이므로) — 전기를 늘리는 일이 곧 땅을 넓히는 일이 된다.
		/// </summary>
		public bool TryPlaceGenerator(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;
			int generatorCost = CostOf(TowerDefensePlaceableKind.Generator);
			if (core.TrySpend(generatorCost) == false)
				return Reject($"자원 부족 {core.Resource}/{generatorCost}", worldPosition);

			occupiedCells.Add(cellKey);
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.HarvesterUnit, null, worldPosition, isHarvester: false, incomeMultiplier: 1f,
				towerArchetype: null, isOuterNode: false, isGenerator: true));
			return true;
		}

		/// <summary>
		/// 코어에서 연구를 한 단계 올린다 — 정수로 산다(사용자 지시: "연구소 건물 없애고, 코어 건물에서
		/// 연구를 진행할 수 있게").
		///
		/// ★ 왜 건물을 없앴나: 짓는 것(자리를 차지하고 지켜야 하는 것)과 키우는 것(판 전체에 걸리는 것)은
		///   성격이 다른 행위인데 같은 핫바에 섞여 있었다. 연구를 코어에 두면 「어디에 지을까」를 고민할
		///   필요 없는 대신 *코어를 지키는 이유*가 하나 더 늘어난다.
		/// 값은 단계마다 오른다 — 무한히 싸게 쌓이면 그건 선택이 아니다.
		/// </summary>
		/// <summary>
		/// 값 없이 연구 한 단계 — **성좌의 큰 마디를 뚫었을 때** 부른다.
		///
		/// ★ 왜 값이 없나: 마디를 찍을 때 이미 정수를 치렀다. 여기서 또 받으면 한 번 뚫는 데 두 번 낸다.
		/// ★ 왜 필요한가: 건물 해금은 연구 *단계*가 정한다. 성좌가 단계를 못 올리면 「성좌를 다 뚫었는데
		///   지을 수 있는 건 그대로」가 되어, 연구창이 판을 바꾸지 못한다.
		/// </summary>
		public void GrantResearchLevel()
		{
			LabCount++;
			RefreshAvailableSlots();
			SlotsChanged();
			if (coreCombatant != null)
				PopWorldText("연구 " + LabCount + "단계", coreCombatant.Position, TextType.Exp);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 성좌로 연구 {LabCount}단계 — 새 칸이 열린다.");
		}

		public bool TryResearch()
		{
			if (core == null || stage == null)
				return false;

			int cost = ResearchCost;
			// ★ 초반 연구는 *일반 자원*으로 산다(사용자 지시). 정수는 바깥 노드에서만 나는데, 그걸
			//   초반 해금의 통로로 두면 「연구로 하나씩 연다」가 시작부터 잠긴다 — 실제로 그랬다.
			//   고급 테크(정수 단계)부터가 개척을 강요하는 자리다.
			if (ResearchUsesEssence)
			{
				if (core.TrySpendEssence(cost) == false)
				{
					if (coreCombatant != null)
						Reject(EssenceShortText(cost), coreCombatant.Position);
					return false;
				}
			}
			else if (core.TrySpend(cost) == false)
			{
				if (coreCombatant != null)
					Reject($"자원 부족 {core.Resource}/{cost}", coreCombatant.Position);
				return false;
			}

			LabCount++;
			RefreshAvailableSlots();
			SlotsChanged();
			if (coreCombatant != null)
				PopWorldText("연구 " + LabCount + "단계", coreCombatant.Position, TextType.Exp);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 {LabCount}단계 — 모든 포탑 피해 배수 {TowerDamageMultiplier:F2}");
			return true;
		}

		/// <summary> 다음 연구가 정수를 먹나(고급 테크) — 아니면 일반 자원이다. </summary>
		public bool ResearchUsesEssence => stage != null && LabCount + 1 >= stage.ResearchEssenceFromLevel;

		/// <summary> 다음 연구 단계 값 — 단계마다 오른다. 초반은 자원, 고급 테크부터 정수. </summary>
		public int ResearchCost
		{
			get
			{
				if (stage == null)
					return 0;
				int baseCost = ResearchUsesEssence ? stage.LabEssenceCost : stage.LabResourceCost;
				return Mathf.Max(1, Mathf.RoundToInt(baseCost * (LabCount + 1) * boons.ResearchCostMultiplier));
			}
		}

		/// <summary>
		/// 지금 쓸 수 있는 칸 — 화면(핫바)과 입력(배치)이 *같은 목록*을 읽는다.
		///
		/// ★ 여기가 해금의 단일 정본이다. 예전엔 칸 번호 → 종류가 고정 산술로 두 곳에 박혀 있어,
		///   해금으로 칸 수가 변하는 순간 「함정을 골랐는데 전초기지가 지어진다」가 된다.
		/// 순서는 손이 기억한다 — 새로 열린 것은 *뒤에* 붙는다(앞이 밀리면 손가락이 헛나간다).
		/// </summary>
		public System.Collections.Generic.IReadOnlyList<TowerDefenseSlot> AvailableSlots => availableSlots;

		private readonly System.Collections.Generic.List<TowerDefenseSlot> availableSlots = new();

		/// <summary> 해금 목록을 다시 만든다 — 연구 단계가 오를 때·판이 시작할 때. </summary>
		private void RefreshAvailableSlots()
		{
			availableSlots.Clear();
			if (stage == null)
				return;

			// ★ 해금 계산은 여기 없다 (WM-200) — 연구 창도 같은 것을 알아야 하는데, 각자 계산하면
			//   *창이 약속한 것과 실제로 열리는 것이 어긋난다*. 표는 하나고, 규칙층은 「여기까지」를
			//   잘라 쓰기만 한다.
			TowerDefenseUnlockSchedule.Available(UnlockLevels, TowerArchetypeCount, LabCount, unlockScratch, availableSlots);

			// ★ 영웅은 핫바에서 뺐다(사용자 지시: "영웅 이동 따로 핫바 두지 않았으면"). 핫바는
			//   *짓는 것*의 자리인데 영웅은 보내는 것이라 뜻이 어긋났고, WASD(시점)와도 헷갈렸다.
			//   이제 빈 땅 우클릭이 영웅을 보낸다 — 대상이 있으면 판매, 없으면 이동(RTS 관용).
		}

		private readonly System.Collections.Generic.List<TowerDefenseUnlockEntry> unlockScratch = new();

		/// <summary> 무대가 정한 해금 단계 수치 — 계산은 순수 표가 한다. </summary>
		private TowerDefenseUnlockLevels UnlockLevels => new(
			stage.TowerUnlockLevel, stage.WallUnlockLevel, stage.TrapUnlockLevel,
			stage.GeneratorUnlockLevel, stage.OutpostUnlockLevel, stage.TowerVariantUnlockStep);

		/// <summary> 해금이 바뀌었다 — 화면이 핫바를 다시 그려야 한다. </summary>
		public event System.Action SlotsChanged = delegate { };

		/// <summary> 지금 연구 단계 — 화면이 코어를 골랐을 때 보여준다. </summary>
		public int ResearchLevel => LabCount;

		/// <summary>
		/// 마수가 죽은 자리 *사거리 안*의 포탑들에게 경험치 — 「처치 관여」(사용자 지시).
		///
		/// ★ 왜 마지막 한 방이 아니라 관여인가: 마지막 타격만 세면 연사 포탑이 경험치를 독식하고,
		///   길목을 지키느라 계속 쏘던 포탑이 아무것도 못 받는다. 관여로 세면 *자리를 잘 잡은 것*이 자란다.
		/// </summary>
		private void AwardKillExperience(Vector3 deathPosition)
		{
			if (stage == null || stage.KillExperience <= 0)
				return;

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				if (doll.IsAlive == false || doll.IsHarvester)
					continue;

				TowerDefenseWeapon weapon = doll.Anchor.GetComponent<TowerDefenseWeapon>();
				if (weapon == null)
					continue;
				if ((doll.Anchor.position.ToSim() - deathPosition).sqrMagnitude > weapon.Range * weapon.Range)
					continue;

				doll.Progress.AddExperience(Mathf.RoundToInt(stage.KillExperience * boons.ExperienceMultiplier));
			}
		}

		/// <summary> 정산 때 채집 인형에게 경험치 — 캐는 것도 일이다. </summary>
		private void AwardHarvestExperience()
		{
			if (stage == null || stage.HarvestExperience <= 0)
				return;

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				if (doll.IsAlive == false || doll.IsHarvester == false)
					continue;
				if (doll.Disconnected || doll.Unpowered)
					continue; // 멈춘 채집은 배우지도 않는다.

				doll.Progress.AddExperience(Mathf.RoundToInt(stage.HarvestExperience * boons.ExperienceMultiplier));
			}
		}

		/// <summary>
		/// 확인용 코어 경험치 — 카드가 실제로 뜬 화면을 재려면 레벨이 올라야 한다.
		/// ★ 값만 준다 — 카드가 나오는 규칙(무엇이 몇 장 나오나)은 그대로 통과시켜야 확인이 의미가 있다.
		/// </summary>
		public void GrantCoreExperienceForVerification(int amount)
		{
			AwardCoreExperience(amount);
		}

		/// <summary>
		/// 확인용 건물 경험치 — 강화 선택지가 실제로 걸린 화면을 재려면 그 건물이 자라야 한다.
		/// ★ 값만 준다 — 무엇이 몇 장 나오나는 그대로 통과시켜야 확인이 의미가 있다.
		/// </summary>
		public bool GrantBuildingExperienceForVerification(MatchCombatant combatant, int amount)
		{
			TowerDefenseDollLabel doll = FindDoll(combatant);
			if (doll == null)
				return false;

			doll.Progress.AddExperience(amount);
			return true;
		}

		/// <summary> 그 자리에 선 인형의 이름표 — 복원이 방금 세운 것을 다시 찾는다. </summary>
		private TowerDefenseDollLabel FindDollLabel(Vector3 worldPosition)
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.IsAlive && (label.Anchor.position.ToSim() - worldPosition).sqrMagnitude <= 1f)
					return label;
			}
			return null;
		}

		/// <summary> 코어 경험치 — 레벨이 오르면 판 전체에 걸리는 선택지가 쌓인다. </summary>
		private void AwardCoreExperience(int amount)
		{
			int before = coreProgress.Level;
			coreProgress.AddExperience(amount);
			if (coreProgress.Level > before && coreCombatant != null)
				PopWorldText("코어 Lv." + coreProgress.Level, coreCombatant.Position, TextType.Exp);
		}

		/// <summary> 코어 레벨 / 이번 구간 진행 / 아직 안 고른 선택지 수 — 화면이 읽는다. </summary>
		public int CoreLevel => coreProgress.Level;
		public float CoreLevelRatio => coreProgress.LevelRatio;

		/// <summary>
		/// 코어가 지금 내놓는 카드들 — 레벨이 씨앗이라 같은 레벨이면 언제 열어도 같은 세 장이다.
		/// 판을 멈추지 않는다(실시간) — 고를 때까지 카드가 코어에 붙어 기다린다.
		/// </summary>
		public void OfferCoreCards(List<TowerDefenseBoon> result)
		{
			result.Clear();
			if (stage == null || coreProgress.PendingChoices <= 0)
				return;

			TowerDefenseDraft.Offer(coreProgress.Level, MapSeed, stage.DraftRules, result);
		}

		/// <summary> 코어 카드 한 장 선택 — 고른 것은 판 전체에 걸린다. </summary>
		public bool ChooseCoreCard(int index)
		{
			List<TowerDefenseBoon> offers = new();
			OfferCoreCards(offers);
			if (index < 0 || index >= offers.Count)
				return false;
			if (coreProgress.Choose(TowerDefenseBuildingPerk.Damage) == false)
				return false; // 대기 하나 소비(어떤 것을 골랐는지는 아래 boons 가 기억한다).

			TowerDefenseBoon boon = offers[index];
			boons.Take(boon);

			switch (boon.Kind)
			{
				case TowerDefenseBoonKind.Life:
					core.AddLives(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Essence:
					core.AddEssence(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.Windfall:
					core.AddResource(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.PowerCapacity:
					bonusPowerCapacity += Mathf.RoundToInt(boon.Magnitude);
					break;
				case TowerDefenseBoonKind.MaxLives:
					core.AddLives(Mathf.RoundToInt(boon.Magnitude));
					break;
				case TowerDefenseBoonKind.CoreRepair:
					RepairCore(boon.Magnitude);
					break;
				default:
					break;
			}

			core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));
			if (coreCombatant != null)
				PopWorldText("「" + boon.DisplayName + "」", coreCombatant.Position, TextType.Heal);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 코어 선택 — {boon.DisplayName} ({boon.Note})");
			return true;
		}

		// 고른 것을 실제 수치에 건다 — 화면만 바뀌고 실물이 그대로면 그건 선택이 아니다.
		private void ApplyPerk(TowerDefenseDollLabel doll, TowerDefenseBuildingPerk perk)
		{
			TowerDefenseWeapon weapon = doll.Anchor.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
			{
				weapon.ApplyPerk(perk, stage.PerkStep);
				// 사거리를 올렸으면 원도 그 자리에서 자란다 — 다음 승급까지 기다리면 그동안 원이 거짓말한다.
				RefreshTowerRing(doll.Anchor.gameObject);
			}

			if (perk == TowerDefenseBuildingPerk.Endure)
			{
				UnitObject unit = doll.Anchor.GetComponent<UnitObject>();
				if (unit != null)
				{
					int bonus = Mathf.Max(1, Mathf.RoundToInt(unit.UnitStat[UnitStatType.HP_MAX] * stage.PerkStep));
					unit.UnitStat[UnitStatType.HP_MAX] += bonus;
					unit.UnitStat[UnitStatType.HP_CUR] += bonus;
				}
			}
		}

		// 카드로 늘린 전기 용량 — 코어가 대주는 양에 더해진다.
		private int bonusPowerCapacity;

		/// <summary> 코어를 최대 체력의 비율만큼 즉시 회복(카드). </summary>
		private void RepairCore(float ratio)
		{
			if (coreCombatant == null || coreCombatant.UnitObject == null)
				return;

			UnitHealth health = coreCombatant.UnitObject.GetComponent<UnitHealth>();
			if (health == null)
				return;

			int amount = Mathf.Max(1, Mathf.RoundToInt(coreCombatant.UnitObject.UnitStat[UnitStatType.HP_MAX] * ratio));
			health.ReceiveHeal(amount);
			PopWorldText("+" + amount, coreCombatant.Position, TextType.Heal);
		}

		/// <summary> 이 건물이 전기를 받고 있나 — 채집 수입이 이 값을 본다. </summary>
		private bool IsPowered(Transform building)
		{
			if (stage == null || stage.CorePowerCapacity <= 0)
				return true;

			return powerGrid.IsPowered(building);
		}

		/// <summary>
		/// 내 것이 판 끝에 다가오면 판을 넓힌다 — *무한 맵의 실체*.
		///
		/// ★ 왜 「넓히기」만 하고 「옮기기」는 안 하나: 창의 원점을 옮기면 이미 저장된 좌표(점유 칸·벽·
		///   전초기지·채집)가 전부 밀린다. 한 곳이라도 안 옮기면 조용히 어긋나는데, 그 병은 이 작업에서
		///   이미 두 번 겪었다(좌표 키 drift / 반경 무음 잠김). 넓히기만 하면 **기존 좌표가 그대로 유효**하다.
		/// ★ 지형은 다시 안 만든다 — 좌표에서 파생되므로 넓힌 자리의 지형은 원래부터 거기 있던 것과 같다.
		///   그래서 넓혀도 이미 본 자리가 변하지 않는다(그게 「경계 없는 지형」을 먼저 만든 이유다).
		/// ★ 다시 세우는 것은 창에 묶인 것들뿐: 격자(암반 목록) · 길찾기 · 안개 · 지면 · 바위.
		/// </summary>
		private void TryGrowWindow()
		{
			if (stage == null || stage.WindowGrowMargin <= 0 || mapLayout == null || windowGrowing)
				return;
			if (CellsToWindowEdge > stage.WindowGrowMargin)
				return;

			windowGrowing = true;
			StartCoroutine(GrowWindowRoutine());
		}

		private bool windowGrowing;

		private IEnumerator GrowWindowRoutine()
		{
			// ★ 확장은 판 전체를 다시 세우는 일이라 *반드시 잰다* — 여기서 프레임이 튀면 무한 맵은
			//   「넓어질 때마다 게임이 멈추는」 것이 된다. 재두면 나중에 무거워져도 바로 안다.
			float growStartedAt = Time.realtimeSinceStartup;
			int newWidth = mapLayout.Width + stage.WindowGrowStep;
			int newLength = mapLayout.Length + stage.WindowGrowStep;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 판이 자란다 — {mapLayout.Width} → {newWidth}칸 (내 것이 끝에서 {CellsToWindowEdge}칸).");

			// ★ 원점을 유지한 채 +방향으로만 넓힌다 — 기존 좌표가 그대로 살아야 한다.
			// ★ 판 전체를 다시 만들지 않는다(실측 981ms) — 지형은 좌표에서 나오므로 *새 띠만* 묻는다.
			TowerDefenseMapParameters parameters = stage.MapParameters.Normalized();
			int siteSpacing = Mathf.Max(2, Mathf.RoundToInt(
				Mathf.Sqrt(mapLayout.Width * (float)mapLayout.Length / Mathf.Max(1, parameters.RockSiteCount))));
			TowerDefenseInfiniteTerrain terrain = new(
				mapLayout.Seed, mapLayout.CoreCell, siteSpacing,
				parameters.RidgeWidth, parameters.ObstacleDensity, parameters.CoreClearRadius);

			TowerDefenseVision olderVision = vision;
			mapLayout = TowerDefenseMapLayout.Grown(mapLayout, newWidth, newLength, terrain.IsBlocked);

			activeGroundWidth = mapLayout.GroundWidth;
			activeGroundLength = mapLayout.GroundLength;

			yield return null;
			if (core == null)
				yield break;

			// 창에 묶인 것들만 다시 세운다 — 지형 자체는 좌표에서 나오므로 이미 본 자리는 안 변한다.
			vision = new TowerDefenseVision(mapLayout.Width, mapLayout.Length);
			vision.CopyExploredFrom(olderVision); // 가봤던 곳이 통째로 어두워지지 않게.
			if (fogView != null)
			{
				Destroy(fogView.gameObject);
				fogView = null;
			}
			fogView = TowerDefenseFogView.Create(
				stageRoot, mapLayout.Width, mapLayout.Length, activeGroundWidth, activeGroundLength, stage.FogHeight);

			RebuildPathing();
			RefreshVision();
			windowGrowing = false;
			float grewInMs = (Time.realtimeSinceStartup - growStartedAt) * 1000f;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 판 확장 끝 — 이제 {mapLayout.Width}칸 "
				+ $"(걸린 시간 {grewInMs:F0}ms, 암반 {mapLayout.ObstacleCells.Count}칸).");
		}

		/// <summary>
		/// 지금 열려 있는 창 안인가 — 창 밖은 「암반」이 아니라 「아직 안 열린 곳」이다(무한 맵 1단계).
		/// 고정 판(생성 안 씀)에서는 경계가 없으므로 언제나 참.
		/// </summary>
		public bool IsInsideWindow(Vector3 worldPosition)
		{
			if (mapLayout == null || stageRoot == null)
				return true;
			return mapLayout.IsInsideWindow(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim());
		}

		/// <summary> 내 것 중 가장 바깥이 창 가장자리에서 몇 칸 남았나 — 창을 넓힐 시점을 정하는 값. </summary>
		public int CellsToWindowEdge
		{
			get
			{
				if (mapLayout == null || stageRoot == null)
					return int.MaxValue;

				int nearest = int.MaxValue;
				foreach (Transform building in supplyChain.Buildings)
				{
					if (building == null)
						continue;
					int distance = mapLayout.CellsToWindowEdge(stageRoot.InverseTransformPoint(building.position).ToSim());
					if (distance < nearest)
						nearest = distance;
				}
				return nearest;
			}
		}

		/// <summary>
		/// 실제로 쓰는 보급 거리 — 설정값과 *판 크기에서 파생한 값* 중 큰 쪽.
		///
		/// ★ 왜 파생시키나 (같은 병을 두 번 앓았다): 절대값으로 박아두면 판을 키울 때마다 상대적으로
		///   짧아져 **기능이 통째로 무음 잠김**된다. 44칸 시절 7 → 바깥 노드가 어떤 사슬로도 안 닿아
		///   정수가 영영 0. 12로 고쳤더니 판을 200칸으로 키우며 같은 일이 재발해 채집이 0기가 됐다.
		///   판 크기가 반경을 모르는 것이 진짜 근본이라, 판이 커지면 반경도 저절로 따라오게 묶는다.
		/// </summary>
		public float EffectiveSupplyReach
		{
			get
			{
				if (stage == null)
					return 0f;

				float derived = Mathf.Min(activeGroundWidth, activeGroundLength) * stage.SupplyReachRatio;
				return Mathf.Max(stage.SupplyReach, derived) * boons.SupplyReachMultiplier
					* (1f + ResearchBonus(TowerDefenseResearchEffect.SupplyReach));
			}
		}

		/// <summary> 그 자리 인형의 이름표(없으면 null) — 승급 단계 표시 갱신에 쓴다. </summary>
		private TowerDefenseDollLabel FindDollLabel(Transform anchor)
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.Anchor == anchor)
					return label;
			}
			return null;
		}

		/// <summary> 세운 연구 인형 수 — 늘어날수록 모든 포탑이 강해진다. </summary>
		public int LabCount { get; private set; }

		// 연구 성좌에서 찍어 모은 것 — 효과 종류별 누적 비율. 화면(성좌)이 고르고, 값은 여기 쌓인다.
		private readonly Dictionary<TowerDefenseResearchEffect, float> researchBonus = new();

		// ★ 아래 셋을 듣는 것은 *성좌 화면이 아니라 판 진행자*다. 화면은 사람이 처음 열 때야 생기는데
		//   이어하기는 그보다 먼저 일어나므로, 화면이 들고 있으면 되돌릴 곳이 없어 저장에 적힌
		//   연구가 통째로 조용히 사라진다. 규칙은 화면 유무와 무관해야 한다.

		/// <summary> 새 판 — 찍은 마디도 처음으로 되돌리라는 신호. </summary>
		public event System.Action ResearchReset = delegate { };

		/// <summary> 저장할 때 「지금 찍혀 있는 마디들」을 받아 적는 통로. </summary>
		public event System.Action<List<int>> CollectResearch = delegate { };

		// 셋을 부르는 자리는 저장·이어하기·새 판뿐이라 밖에서 부를 일이 없지만, 검사기가
		// 「화면 없이도 되돌아오나」를 재려면 저장 경로와 *똑같은 문*으로 들어와야 한다
		// (검사 전용 뒷문을 따로 내면 그 문만 멀쩡하고 진짜 경로는 썩어도 모른다).
		public void ClearResearch()
		{
			researchBonus.Clear();
			ResearchReset();
			if (core != null)
				core.IncomeMultiplier = boons.IncomeMultiplier;
			RefreshRangeRings();
			RefreshSupplyRings();
		}

		/// <summary> 화면에 실제로 그려진 보급 원의 반지름 — 규칙이 아니라 *사람이 보는 것*을 잰다. </summary>
		public float DrawnSupplyReach
		{
			get
			{
				foreach (TowerDefenseRing ring in supplyRings)
				{
					if (ring != null)
						return ring.Radius;
				}

				return 0f;
			}
		}

		/// <summary> 보급 원점의 원을 지금 보급 거리로 다시 그린다 — 안 그리면 「어디까지 지어지나」가 거짓말한다. </summary>
		private void RefreshSupplyRings()
		{
			float reach = EffectiveSupplyReach;
			for (int index = supplyRings.Count - 1; index >= 0; index--)
			{
				if (supplyRings[index] == null)
				{
					supplyRings.RemoveAt(index);
					continue;
				}

				supplyRings[index].SetRadius(reach);
			}
		}

		public void CollectResearchInto(List<int> into) => CollectResearch(into);

		/// <summary> 그 종류로 지금까지 얼마나 세졌나(0.2 = +20%). </summary>
		public float ResearchBonus(TowerDefenseResearchEffect effect)
		{
			return researchBonus.TryGetValue(effect, out float amount) ? amount : 0f;
		}

		/// <summary>
		/// 성좌에서 마디 하나를 찍는다 — 값을 치르고 효과를 쌓는다.
		/// 값이 모자라면 아무 일도 안 일어난다(화면이 찍힌 척하면 안 되므로 false 를 돌려준다).
		/// </summary>
		/// <summary>
		/// 정수가 모자랄 때 하는 말 — **어디서 버는지까지** 한 곳에서 만든다.
		///
		/// ★ 사용자 실증: "정수 어떻게 얻어? 강화를 할 수가 없는데?" — 화면은 「부족」만 말하고
		///   *버는 법*을 어디서도 말하지 않았다. 값이 모자라다는 것은 이미 눈에 보이는 사실이고,
		///   사람이 막히는 지점은 「그럼 어떻게 벌지」다. 세 갈래를 그 자리에서 말한다.
		/// ★ 한 곳에서 만드는 이유: 정수를 쓰는 자리가 넷(승급·연구 인형·성좌·전초기지)인데
		///   따로 적으면 하나만 고쳐도 나머지가 옛말을 한다.
		/// </summary>
		/// <summary> 정수를 깎는다(검증 전용) — 「모자랄 때 뭐라고 하나」는 모자라게 만들어야 잴 수 있다. </summary>
		public void SpendEssenceForVerification(int amount)
		{
			if (core != null && amount > 0)
				core.TrySpendEssence(amount);
		}

		private string EssenceShortText(int cost)
		{
			return $"정수 부족 {core.Essence}/{cost} — 바깥 광맥 채집 · 둥지 부수기 · 서식지 소탕";
		}

		public bool TryTakeResearchNode(TowerDefenseResearchEffect effect, float amount, int cost, bool usesEssence)
		{
			if (core == null)
				return false;
			// ★ 「연구값 할인」 카드를 여기 태운다. 그 카드는 옛 연구(단추 한 번에 한 단계)의 값에만
			//   걸려 있었는데, 연구가 성좌로 옮겨오면서 **걸릴 곳이 없어져 아무 효과도 없는 카드**가 됐다.
			//   화면엔 「연구값↓」이라 적히는데 실제로는 한 푼도 안 깎이는 상태였다 — 카드가 거짓말한다.
			cost = Mathf.Max(0, Mathf.RoundToInt(cost * boons.ResearchCostMultiplier));

			// ★ 안쪽 고리는 일반 자원으로 산다 (사용자 실증: "연구 자원이 정수면 초반에 연구 어떻게
			//   하라는 겁니까"). 정수는 바깥으로 나가야 나는 것이라, 그걸 첫 마디의 통로로 두면
			//   판 시작에 연구가 통째로 잠긴다. 개척을 강요하는 자리는 바깥 고리다.
			if (cost > 0)
			{
				bool paid = usesEssence ? core.TrySpendEssence(cost) : core.TrySpend(cost);
				if (paid == false)
				{
					string lack = usesEssence ? EssenceShortText(cost) : $"자원 부족 {core.Resource}/{cost}";
					if (coreCombatant != null)
						Reject(lack, coreCombatant.Position);
					Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 거절 — {lack}.");
					return false;
				}
			}

			researchBonus.TryGetValue(effect, out float current);
			researchBonus[effect] = current + amount;

			// ★ 채집 수입 배수는 *카드를 뽑을 때만* 다시 계산되고 있었다 — 연구로 올려도 다음 카드가
			//   나올 때까지 판은 그대로였다(라이브 검증에서 40 → 40 으로 잡힘).
			//   여기서 같이 갱신한다. 「물을 때마다 읽는」 다른 갈래와 달리 이건 한 번 써두는 값이라,
			//   바뀌는 자리마다 다시 써주지 않으면 조용히 옛 값으로 돈다.
			if (core != null)
				core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));

			// 같은 병 — 원은 지을 때 한 번 그려진다. 다시 안 그리면 총만 멀리 나가고 보급만 멀리 닿는다.
			if (effect == TowerDefenseResearchEffect.TowerRange)
				RefreshRangeRings();
			if (effect == TowerDefenseResearchEffect.SupplyReach)
				RefreshSupplyRings();

			// ★ 코어 방어만 *찍는 순간* 몸에 새긴다. 다른 갈래는 「물을 때마다 읽는」 배수라 저절로
			//   반영되지만, 체력은 이미 정해진 값이라 아무도 다시 묻지 않는다 — 여기서 안 올리면
			//   찍어도 아무 일이 안 일어난다(코어 방어만 조용히 죽은 갈래가 된다).
			if (effect == TowerDefenseResearchEffect.CoreArmor && coreCombatant != null
				&& coreCombatant.UnitObject != null)
			{
				UnitStat stat = coreCombatant.UnitObject.UnitStat;
				int added = Mathf.Max(1, Mathf.RoundToInt(stat[UnitStatType.HP_MAX] * amount));
				stat[UnitStatType.HP_MAX] += added;
				stat[UnitStatType.HP_CUR] += added; // 늘린 만큼 실제로 채워준다 — 최대치만 늘면 체감이 0이다.
				PopWorldText("코어 +" + added, coreCombatant.Position, TextType.Heal);
			}
			Debug.Log($"{nameof(TowerDefenseMatch)}: 연구 {TowerDefenseResearchGraph.NameOf(effect)} "
				+ $"+{amount:P0} → 누적 {researchBonus[effect]:P0}");
			return true;
		}

		/// <summary> index 번 포탑의 건설 비용 — 종류가 없으면 스테이지 기본값. </summary>
		/// <summary>
		/// 그 종류를 *지금* 세우는 데 드는 값 — 화면과 규칙이 같은 창구에 묻는다.
		///
		/// ★ 왜 하나로 모으나: 핫바는 스테이지 원값을 보여주고 배치는 할인값을 뗐다.
		///   건설 할인 카드를 고른 순간 **화면은 40 이라 말하고 지갑에선 34 가 빠졌다** — 화면이 거짓말한다.
		/// ★ 게다가 할인이 경로마다 다르게 걸려 있었다(포탑·채집·발전만, 함정·벽은 안 걸림).
		///   카드에는 「건설 비용 할인」이라 적혀 있는데 절반한테만 걸리면 그건 규칙이 아니라 사고다.
		/// 정수로 사는 것(전초기지·연구)은 자원 할인과 다른 통장이라 여기서 갈라 답한다.
		/// </summary>
		public int CostOf(TowerDefensePlaceableKind kind, int towerIndex = 0)
		{
			if (stage == null)
				return 0;

			switch (kind)
			{
				case TowerDefensePlaceableKind.Tower:
					return Discounted(TowerCostAt(towerIndex));
				case TowerDefensePlaceableKind.Harvester:
					return Discounted(stage.HarvesterCost);
				case TowerDefensePlaceableKind.Wall:
					return Discounted(stage.WallCost);
				case TowerDefensePlaceableKind.Trap:
					return Discounted(stage.TrapCost);
				case TowerDefensePlaceableKind.Generator:
					return Discounted(stage.GeneratorCost);
				// 정수로 산다 — 자원 할인은 안 걸린다(다른 통장).
				case TowerDefensePlaceableKind.Outpost:
					return stage.OutpostEssenceCost;
				// 영웅은 짓는 게 아니라 보내는 것 — 값이 없다.
				default:
					return 0;
			}
		}

		/// <summary> 카드 할인이 걸린 실제 값 — 화면의 값과 실제 차감이 같은 곳을 읽는다. </summary>
		public int Discounted(int cost) => Mathf.Max(1, Mathf.RoundToInt(cost * boons.CostMultiplier));

		/// <summary>
		/// 보급 다시 계산 + 수입 반영. 건물이 서거나 사라질 때마다, 그리고 매 틱 부른다.
		/// 끊긴 채집은 수입이 0 — 「넓히면 번다」가 「넓히면 지킬 것이 는다」로 바뀌는 지점.
		/// </summary>
		/// <summary>
		/// 확인 도구 전용 — 지금 당장 다시 세게 한다. **판이 매 틱 부르는 그 셈 그대로**라
		/// 검사 전용 셈이 따로 생기지 않는다(그러면 그쪽만 멀쩡하고 진짜 경로가 썩어도 모른다).
		/// </summary>
		public void RefreshSupplyForVerification() => RefreshSupply();

		private void RefreshSupply()
		{
			if (core == null || coreCombatant == null || stage == null)
				return;

			// 「누가 이어졌나」는 사슬이 답한다 — 여기 남는 것은 「그래서 얼마 버나」뿐이다.
			supplyChain.Compute(coreCombatant.Position, outposts, EffectiveSupplyReach);

			float resourceWeight = 0f;
			float essenceWeight = 0f;
			DisconnectedHarvesters = 0;
			WorkingHarvesters = 0;
			OuterHarvesters = 0;
			SuppliedOuterHarvesters = 0;
			PoweredOuterHarvesters = 0;

			IReadOnlyList<Transform> chain = supplyChain.Buildings;
			for (int index = 0; index < chain.Count; index++)
			{
				Transform building = chain[index];
				bool connected = supplyChain.IsConnected(index);
				bool outer = harvesterIsOuter.TryGetValue(building, out bool isOuter) && isOuter;

				if (outer)
				{
					OuterHarvesters++;
					if (connected)
					{
						SuppliedOuterHarvesters++;
						// 보급과 전기는 *다른 관문*이다 — 위 벌이 계산은 전기도 요구하는데 여기서 안 세면
						// 「이어졌는데 정수가 0」이라는 거짓 실패가 찍히고 진짜 이유(전기 없음)가 안 보인다.
						if (IsPowered(building))
							PoweredOuterHarvesters++;
					}
				}

				if (harvesterTransforms.Contains(building) == false)
					continue;

				// 끊긴 사실을 그 인형 머리 위에 붙인다 — 수입이 왜 안 오는지가 숫자가 아니라 *자리*로 보여야 한다.
				TowerDefenseDollLabel label = FindDollLabel(building);
				if (label != null)
					label.Disconnected = connected == false;

				if (connected == false)
				{
					DisconnectedHarvesters++;
					continue;
				}

				if (IsPowered(building) == false)
					continue; // 전기가 끊긴 채집은 캐지 못한다.

				WorkingHarvesters++; // 여기까지 온 것만 실제로 번다 — 화면이 이 수를 말해야 정직하다.

				float multiplier = HarvesterMultiplierOf(building);
				if (outer)
					essenceWeight += multiplier;
				else
					resourceWeight += multiplier;
			}

			core.SetHarvesterWeights(resourceWeight, essenceWeight);
		}

		/// <summary> 보급이 끊긴 채집 인형 수 — 화면이 「왜 수입이 줄었나」를 말해줘야 한다. </summary>
		public int DisconnectedHarvesters { get; private set; }

		/// <summary>
		/// *실제로 버는* 채집 인형 수 — 보급도 이어졌고 전기도 들어온 것만.
		/// ★ 화면이 「채집 N기」라며 지은 수를 말하면, 다섯 채 중 둘만 일해도 다섯이라고 한다.
		///   그러면 「왜 수입이 이것밖에 안 되지」가 영영 안 풀린다.
		/// </summary>
		public int WorkingHarvesters { get; private set; }

		/// <summary> 보급 사슬 후보 건물 수 — 「사슬이 비었나 / 안 닿나」를 가르는 진단값. </summary>
		public int SupplyBuildingCount => supplyChain.Buildings.Count;

		/// <summary>
		/// 바깥 노드에 선 채집 수 / 그중 보급이 이어진 수.
		/// ★ 정수가 0일 때 원인이 셋 중 어느 것인지 갈라준다: ① 바깥에 안 세웠다 ② 세웠는데 안 이어졌다
		///   ③ 둘 다 됐는데 안 들어온다(진짜 결함). 이 구분이 없으면 「바깥 노드인데 정수가 안 나온다」 같은
		///   *거짓 실패*가 계속 찍힌다(실측: 실제로는 바깥에 세운 적이 없었다).
		/// </summary>
		/// <summary>
		/// 이 판에 *바깥 등급* 광맥이 몇 개나 있나 — 정수가 날 수 있는 자리의 총수.
		/// ★ 이걸 안 보면 「바깥에 세운 게 없음」이 사람 탓인지 판 탓인지 갈리지 않는다(실측에서 갈렸다).
		/// </summary>
		/// <summary>
		/// 바깥 등급 광맥의 자리들 — 「멀다」와 「바깥 등급이다」는 다르다(거리로 고르면 매번 안쪽을 집는다).
		/// ★ **무대 기준 좌표**다(월드 아님). 월드 좌표와 섞어 재면 거리가 1900 같은 헛수가 나온다 — 실측.
		/// </summary>
		public void CollectOuterNodeLocalPositions(List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			for (int index = 0; index < activeNodeIsOuter.Count && index < activeNodePositions.Count; index++)
			{
				if (activeNodeIsOuter[index])
					into.Add(activeNodePositions[index]);
			}
		}

		public int OuterNodeCount
		{
			get
			{
				int count = 0;
				foreach (bool isOuter in activeNodeIsOuter)
				{
					if (isOuter)
						count++;
				}

				return count;
			}
		}

		public int OuterHarvesters { get; private set; }
		public int SuppliedOuterHarvesters { get; private set; }

		/// <summary> 그중 전기까지 들어온 수 — 벌이는 보급*과* 전기를 둘 다 요구한다. </summary>
		public int PoweredOuterHarvesters { get; private set; }

		/// <summary>
		/// 그 채집 인형의 벌이 — **처리 범위 안의 광맥 자리 수**로 정해진다(사용자 지시: "자원 건물이
		/// 처리할 수 있는 타일 범위를 만들던지").
		///
		/// ★ 왜 「한 자리 = 한 기」가 아닌가: 자원이 광맥으로 뭉치면서 「어디에 세우나」가 판단이 됐다.
		///   덩어리 한가운데 세우면 여러 자리를 한꺼번에 물고, 가장자리에 세우면 조금만 문다.
		///   자리를 하나만 세는 옛 방식이면 광맥을 만든 의미가 사라진다.
		/// 벌이 배수는 *물고 있는 자리들의 배수 합* — 멀리 있는 큰 광맥일수록 크게 번다.
		/// </summary>
		private float HarvesterMultiplierOf(Transform harvester)
		{
			float reach = stage != null ? stage.HarvesterWorkRadius : 1f;
			float reachSqr = reach * reach;

			float total = 0f;
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index].ToUnity()).ToSim();
				if ((nodeWorld - harvester.position.ToSim()).sqrMagnitude <= reachSqr)
					total += NodeIncomeMultiplierAt(index);
			}

			return (total > 0f ? total : 1f) * boons.HarvestYieldMultiplier;
		}

		/// <summary>
		/// 전초기지 세우기 — 정수로만. 세우는 순간 ① 마수가 향하는 목표가 하나 늘고
		/// ② 보급의 새 원점이 생기고 ③ 시야가 넓어진다. 「넓히면 벌지만 지킬 곳이 는다」가 한 건물에 들어있다.
		/// </summary>
		public bool TryPlaceOutpost(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;
			if (core.TrySpendEssence(stage.OutpostEssenceCost) == false)
				return Reject(EssenceShortText(stage.OutpostEssenceCost), worldPosition);

			occupiedCells.Add(cellKey);

			GameObject outpostObject = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
			outpostObject.name = "Outpost";
			outpostObject.transform.SetParent(stageRoot, false);
			outpostObject.transform.position = (worldPosition + new Vector3(0f, 0.6f, 0f)).ToUnity();
			outpostObject.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f).ToUnity();

			Renderer outpostRenderer = outpostObject.GetComponent<Renderer>();
			if (outpostRenderer != null)
			{
				Material material = new Material(outpostRenderer.sharedMaterial);
				material.color = stage.OutpostTint;
				if (material.HasProperty("_BaseColor"))
					material.SetColor("_BaseColor", stage.OutpostTint);
				outpostRenderer.sharedMaterial = material;
			}

			// ★ 전초기지 자체 방어 — 무기는 *유닛 프리팹으로 세운 수비대*가 든다.
			//   앞서 도형(큐브)에 바로 무기를 달았더니 몸(UnitObject)이 없어 라이브에서 널 참조로 터졌다.
			//   전초기지 표식(큐브)은 길·보급의 앵커로 두고, 그 자리에 *지키는 인형*을 한 기 세운다 —
			//   기존 배치 경로를 그대로 재사용하므로 새로 만드는 것이 없고, 그 인형은 맞을 수도 있다
			//   (「넓힌 곳도 지켜야 한다」가 규칙으로 성립한다).
			if (stage.OutpostWeapon != null && stage.TowerUnit != null && stage.TowerUnit.Prefab != null)
			{
				StartCoroutine(SpawnDefensiveUnitRoutine(
					stage.TowerUnit, null, worldPosition, isHarvester: false, incomeMultiplier: 1f,
					towerArchetype: stage.OutpostWeapon));
			}

			outposts.Add(outpostObject.transform);
			supplyChain.Add(outpostObject.transform);
			ShowSupplyReachRing(outpostObject.transform); // 새 원점이므로 새 사거리 원.
			AddVisionSource(worldPosition, stage.OutpostVisionRadius);
			RebuildPathing(); // 목표가 늘었으므로 마수의 길이 통째로 바뀐다.
			RefreshSupply();
			return true;
		}

		/// <summary> 세운 전초기지 수 — 지킬 곳의 개수. </summary>
		public int OutpostCount => outposts.Count;

		/// <summary> 남은 목숨(유출제 아니면 0). </summary>
		public int Lives => core != null ? core.Lives : 0;

		/// <summary> 이 판이 유출제인가 — 화면이 목숨을 보여줄지 결정한다. </summary>
		public bool UsesLives => core != null && core.UsesLives;

		/// <summary>
		/// 웨이브 정산 내역을 *번 자리에* 띄운다 — 코어에 기본 수입, 채집 인형 각자 머리 위에 자기 몫.
		/// 총액만 HUD 숫자로 올리면 「채집 인형이 무슨 역할인지」가 영원히 안 읽힌다(사용자 실증).
		/// </summary>
		private void ShowIncomeBreakdown()
		{
			if (core == null || stage == null)
				return;

			if (coreCombatant != null && stage.Rules.BaseWaveIncome > 0)
				PopWorldText("+" + stage.Rules.BaseWaveIncome, coreCombatant.Position, TextType.Heal);

			if (stage.Rules.IncomePerHarvester <= 0)
				return;

			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				Transform harvester = harvesterTransforms[index];
				if (harvester == null)
				{
					harvesterTransforms.RemoveAt(index);
					continue;
				}
				// ★ 그 인형이 *실제로 번 만큼*을 띄운다.
				//   예전엔 전부 같은 숫자(정액)를 띄웠다 — 그러면 두 가지가 동시에 거짓말이 된다:
				//   ① 먼 큰 광맥에 세운 인형이 훨씬 많이 버는데 화면은 옆 인형과 같은 수를 보여준다
				//      (「자리를 잘 잡았다」를 배울 유일한 피드백인데 그게 안 보인다)
				//   ② 보급이 끊겼거나 전기가 없어 *한 푼도 못 번* 인형 위에도 숫자가 떴다.
				TowerDefenseDollLabel harvesterLabel = FindDollLabel(harvester);
				if (harvesterLabel != null && (harvesterLabel.Disconnected || harvesterLabel.Unpowered))
					continue; // 멈춘 채집은 아무것도 안 벌었다 — 아무 숫자도 띄우지 않는다.

				int earned = Mathf.RoundToInt(
					stage.Rules.IncomePerHarvester * HarvesterMultiplierOf(harvester) * core.IncomeMultiplier);
				if (earned <= 0)
					continue;

				PopWorldText("+" + earned, harvester.position.ToSim(), TextType.Heal);

				// ★ 바깥 채집은 정수를 낸다 — 그게 「멀리 나간」 보상인데 들어와도 화면이 한 마디도 안 했다.
				//   보이지 않는 보상은 배울 수가 없다(왜 위험을 무릅쓰는지가 안 남는다).
				if (harvesterIsOuter.TryGetValue(harvester, out bool outerNode) == false || outerNode == false)
					continue;

				// 규칙이 쓰는 것과 같은 식으로 — 정수는 자원과 달리 정산 배수가 아니라 채집 가중치만 탄다.
				int essence = Mathf.RoundToInt(
					stage.Rules.EssencePerHarvester * HarvesterMultiplierOf(harvester));
				if (essence > 0)
					PopWorldText("정수 +" + essence, harvester.position.ToSim(), TextType.Exp);
			}
		}

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 채집건물 배치 — 반드시 미점유 자원 노드 반경 내에만 성립
		/// (개척 리스크 = 설계 긴장: 코어 바로 옆에 쌓아 무위험 수입을 얻는 것 차단). 노드 없으면 자원 무변경 false.
		/// 성공 시 core.AddHarvester() 로 다음 정산부터 수입 증가 + 스폰 위치를 노드 좌표로 스냅.
		/// </summary>
		public bool TryPlaceHarvester(Vector3 worldPosition)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.HarvesterUnit == null || stage.HarvesterUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.HarvesterUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}
			if (TryFindPlaceableNode(worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition) == false)
				return Reject("자원 노드 위에만 선다", worldPosition); // 자원 무변경(스펙#C).

			Vector3Int cellKey = ToCellKey(nodeWorldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("이 노드는 이미 잡혔다", nodeWorldPosition);

			if (IsInBuildableRange(nodeWorldPosition) == false)
				return Reject("보급이 닿는 곳에만 지을 수 있다", nodeWorldPosition);
			int harvesterCost = CostOf(TowerDefensePlaceableKind.Harvester);
			if (core.TrySpend(harvesterCost) == false)
				return Reject($"자원 부족 {core.Resource}/{harvesterCost}", nodeWorldPosition);

			claimedNodes.Add(nodeIndex); // TrySpend 성공 후에만 점유 확정(스펙 지시 — 실패 시 점유 안 남김).
			occupiedCells.Add(cellKey);
			float incomeMultiplier = nodeIndex < activeNodeIncomeMultipliers.Count ? activeNodeIncomeMultipliers[nodeIndex] : 1f;
			bool outerNode = nodeIndex < activeNodeIsOuter.Count && activeNodeIsOuter[nodeIndex];
			// 등급은 인형이 실제로 생긴 뒤 *그 인형에* 붙인다(스폰이 코루틴이라 지금은 아직 없다).
			StartCoroutine(SpawnDefensiveUnitRoutine(stage.HarvesterUnit, null, nodeWorldPosition, isHarvester: true, incomeMultiplier,
				towerArchetype: null, isOuterNode: outerNode));
			return true;
		}

		// 판 채집 인형이 잡고 있던 노드를 놓아준다(못 놓으면 그 노드는 영영 못 쓴다).
		private int ReleaseNodeAt(Vector3 worldPosition)
		{
			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index) == false)
					continue;
				Vector3 nodeWorld = stageRoot.TransformPoint(activeNodePositions[index].ToUnity()).ToSim();
				if ((nodeWorld - worldPosition).sqrMagnitude > 1f)
					continue;

				claimedNodes.Remove(index);
				return index;
			}
			return -1;
		}

		/// <summary>
		/// worldPosition 반경 NodeCaptureRadius 내 가장 가까운 *미점유* 자원 노드를 찾는다.
		/// 배치 UI 가 유효/무효 프리뷰를 보여줄 때도 이 메서드로 규칙 중복 없이 재사용(TryPlaceHarvester 와 동일 판정).
		/// </summary>
		public bool TryFindPlaceableNode(Vector3 worldPosition, out int nodeIndex, out Vector3 nodeWorldPosition)
		{
			nodeIndex = -1;
			nodeWorldPosition = Vector3.zero;

			if (stage == null || stageRoot == null)
				return false;

			float captureRadiusSqr = stage.NodeCaptureRadius * stage.NodeCaptureRadius;
			int bestIndex = -1;
			float bestSqrDistance = float.MaxValue;

			for (int index = 0; index < activeNodePositions.Count; index++)
			{
				if (claimedNodes.Contains(index))
					continue;

				Vector3 candidateWorldPosition = stageRoot.TransformPoint(activeNodePositions[index].ToUnity()).ToSim();
				float sqrDistance = (candidateWorldPosition - worldPosition).sqrMagnitude;
				if (sqrDistance > captureRadiusSqr)
					continue;
				if (sqrDistance < bestSqrDistance)
				{
					bestSqrDistance = sqrDistance;
					bestIndex = index;
				}
			}

			if (bestIndex < 0)
				return false;

			nodeIndex = bestIndex;
			nodeWorldPosition = stageRoot.TransformPoint(activeNodePositions[bestIndex].ToUnity()).ToSim();
			return true;
		}
	}
}
