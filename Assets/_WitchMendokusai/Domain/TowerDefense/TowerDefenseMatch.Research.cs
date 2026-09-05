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
	// TowerDefenseMatch 의 연구와 실험 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 자원 노드 점유 — 채집건물은 반드시 미점유 노드를 잡아야 가동(개척 리스크). index = stage.ResourceNodePositions 인덱스.
		private readonly HashSet<int> claimedNodes = new();

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
