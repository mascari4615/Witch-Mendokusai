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
	// TowerDefenseMatch 의 탑과 사거리와 팔기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 세워둔 것들의 사거리 원 — 기본은 전부 꺼져 있고, 묻는 순간(마우스 얹기)에만 하나가 켜진다.
		private readonly List<TowerDefenseRing> rangeRings = new();
		private bool showAllRanges;

		/// <summary>
		/// 그 칸의 포탑을 한 단계 올린다 — 같은 종류일 때만. 값은 기본 비용 × 다음 단계.
		/// 최대 단계거나 다른 종류면 아무 일도 안 일어난다(자원 무변경).
		/// </summary>
		private bool TryUpgradeTowerAt(Vector3Int cellKey, int towerIndex)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype == null)
				return false;

			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position.ToSim()) != cellKey)
					continue;
				// 그 칸을 지나가던 마수가 먼저 잡히면 승급이 조용히 거절된다 — 내가 세운 것만 본다.
				if (supplyChain.Contains(unit.transform) == false)
					continue;

				TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
				// 같은 종류인지는 *정체*로 묻는다 — 값으로 물으면 값이 같은 두 종류가 한 종류로 뭉쳐
				// 엉뚱한 무기가 조용히 승급된다(값은 언제든 같아질 수 있는 수치일 뿐이다).
				if (weapon == null || weapon.Archetype != archetype)
					return false; // 다른 종류(또는 포탑이 아님) — 겹배치 차단 그대로.
				if (weapon.Level >= archetype.MaxLevel)
					return false;

				// 승급도 정수 — 「지금 더 짓기(자원)」 vs 「있는 걸 키우기(정수)」가 서로 다른 통장을 쓴다.
				// 값이 단계마다 얼마나 붙는지는 스테이지가 정한다 — 여기 숫자를 박아두면
				// 밸런스를 만질 때마다 코드를 고쳐야 하고, 화면에 노출된 다른 수치와 갈라진다.
				int upgradeCost = Mathf.Max(1, Mathf.RoundToInt(
					stage.UpgradeEssenceCost * (weapon.Level + 1) * stage.UpgradeCostGrowth));
				if (core.TrySpendEssence(upgradeCost) == false)
				{
					// ★ 조용히 false 를 돌려주면 「눌렀는데 아무 일도 안 일어난다」가 된다 —
					//   사람은 그걸 고장으로 읽는다. 왜 안 되는지와 어떻게 버는지를 그 자리에서 말한다.
					Reject(EssenceShortText(upgradeCost), unit.transform.position.ToSim());
					return false;
				}

				weapon.TryUpgrade();

				// 이름표에도 단계가 붙는다 — 같은 아이가 자란 것이지 새 물건이 생긴 것이 아니다.
				TowerDefenseDollLabel label = FindDollLabel(unit.transform);
				if (label != null)
					label.Level = weapon.Level;

				PopWorldText("Lv." + weapon.Level, unit.transform.position.ToSim(), TextType.Exp);
				RefreshTowerRing(unit);
				return true;
			}

			return false;
		}

		/// <summary>
		/// 사거리가 자라면 화면의 원도 같이 자라야 한다 — 안 그러면 원이 거짓말한다.
		///
		/// ★ 반지름을 여기서 *다시 계산하지 않는다*. 예전엔 승급 배수만 손으로 곱했는데,
		///   그 식에는 강화로 늘어난 사거리가 빠져 있었다 — 사거리 강화를 골라도 원이 그대로였다.
		///   실제로 쏘는 거리를 쥔 쪽(무기)에게 물으면 둘이 갈라질 수가 없다.
		/// </summary>
		private void RefreshTowerRing(GameObject unit)
		{
			TowerDefenseWeapon weapon = unit != null ? unit.GetComponent<TowerDefenseWeapon>() : null;
			TowerDefenseRing ring = unit != null ? unit.GetComponentInChildren<TowerDefenseRing>() : null;
			if (weapon != null && ring != null)
				ring.SetRadius(weapon.Range);
		}

		/// <summary>
		/// 포탑 사거리 = 전술의 표적 탐색 반경. 별도 수치를 두면 화면의 원과 실제 사거리가 갈라진다
		/// (원이 거짓말하는 순간 배치 판단 전체가 무의미해진다) — 그래서 전술 정본에서 읽는다.
		/// </summary>
		public float TowerRange(int towerIndex = 0)
		{
			// ★ 연구 배수를 *여기서* 곱한다. 총은 이 배수를 곱해 쏘는데 원만 안 곱하면, 원은 그대로인데
			//   실제로는 더 멀리 쏘는 「거짓말하는 원」이 된다 — 배치 판단의 유일한 근거가 그 원이다.
			return RawTowerRange(towerIndex) * TowerRangeMultiplier;
		}

		/// <summary> 연구를 빼고 무대가 적어둔 그대로의 사거리 — 배수를 두 번 곱하지 않으려면 여기서 읽는다. </summary>
		public float RawTowerRange(int towerIndex = 0)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			if (archetype != null)
				return archetype.Range;

			if (stage == null || stage.TowerTactic.Rules == null)
				return 0f;

			float best = 0f;
			foreach (TacticRule rule in stage.TowerTactic.Rules)
			{
				if (rule.Target.MaxRange > best)
					best = rule.Target.MaxRange;
			}
			return best;
		}

		// ── 판 기록 ───────────────────────────────────────────────────────────────
		// ★ 왜 필요한가 (개선 목록 24번): 지금은 지고 나면 「몇 분 버팀」 한 줄뿐이라 *왜 졌는지*를
		//   되짚을 수단이 없다. 무엇을 몇 개 지었고, 몇 개를 잃었고, 마수가 가장 많을 때 몇이었는지가
		//   남아야 다음 판이 달라진다 — 안 남으면 매 판이 같은 실수의 반복이 된다.
		// 방금 판 인형들 — 다음 정리에서 「잃음」으로 세지 않기 위한 표시.
		private readonly HashSet<TowerDefenseDollLabel> soldDolls = new();

		/// <summary>
		/// 마우스가 얹힌 건물의 사거리만 켠다(나머지는 끈다). 상시 표시가 정보가 아니라 노이즈가 되는 것을
		/// 막는 유일한 장치 — 「지금 이것」 하나만 보여준다.
		/// </summary>
		public void HighlightRangeOf(Transform unit)
		{
			if (showAllRanges)
				return; // 디버그 토글이 켜져 있으면 전부 보여주는 중 — 손대지 않는다.

			TowerDefenseRing wanted = null;
			if (unit != null)
				wanted = unit.GetComponentInChildren<TowerDefenseRing>(true);

			if (wanted == highlightedRing)
				return;

			if (highlightedRing != null)
				highlightedRing.SetVisible(false);

			highlightedRing = wanted;
			if (highlightedRing != null)
				highlightedRing.SetVisible(true);
		}

		/// <summary> 디버그 — 세워둔 것 전부의 사거리를 한 번에 보여준다/감춘다. </summary>
		public void ToggleAllRanges()
		{
			showAllRanges = showAllRanges == false;

			for (int index = rangeRings.Count - 1; index >= 0; index--)
			{
				if (rangeRings[index] == null)
				{
					rangeRings.RemoveAt(index);
					continue;
				}
				rangeRings[index].SetVisible(showAllRanges);
			}

			highlightedRing = null;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 전체 사거리 표시 {(showAllRanges ? "켜짐" : "꺼짐")}");
		}

		/// <summary> 전체 사거리 표시 중인가 — 화면 버튼이 상태를 보여준다. </summary>
		public bool ShowAllRanges => showAllRanges;

		public float TowerRangeMultiplier => 1f + ResearchBonus(TowerDefenseResearchEffect.TowerRange);

		public float TowerDamageMultiplier =>
			(1f + LabCount * (stage != null ? stage.LabDamageBonus : 0f)) * boons.DamageMultiplier
			* (1f + ResearchBonus(TowerDefenseResearchEffect.TowerDamage));

		/// <summary>
		/// 지어놓은 것들의 사거리 원을 다시 그린다.
		/// ★ 반지름을 여기서 *다시 계산하지 않는다* — 실제로 쏘는 거리를 쥔 무기에게 묻는다.
		///   (원형 반지름을 따로 들고 곱하는 방식도 써봤지만, 그건 무기의 셈을 베낀 두 번째 정본이라
		///   승급·강화가 끼는 순간 또 갈라진다. 배수를 아는 곳은 한 곳이어야 한다.)
		/// </summary>
		private void RefreshRangeRings()
		{
			for (int index = rangeRings.Count - 1; index >= 0; index--)
			{
				if (rangeRings[index] == null)
				{
					rangeRings.RemoveAt(index);
					continue;
				}

				Transform owner = rangeRings[index].transform.parent;
				if (owner != null)
					RefreshTowerRing(owner.gameObject);
			}
		}

		/// <summary> 등록된 포탑 종류 수(0 이면 기존 단일 포탑). </summary>
		public int TowerArchetypeCount => stage != null && stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0;

		/// <summary> 그 종류가 몇 번 칸인가 — 저장이 「무엇을 세웠는지」를 적으려면 번호가 필요하다. </summary>
		private int TowerArchetypeIndexOf(TowerDefenseTowerArchetype archetype)
		{
			if (archetype == null || stage == null || stage.TowerArchetypes == null)
				return 0;

			for (int index = 0; index < stage.TowerArchetypes.Length; index++)
			{
				if (stage.TowerArchetypes[index] == archetype)
					return index;
			}
			return 0;
		}

		/// <summary> index 번 포탑 종류(범위 밖이면 null). </summary>
		public TowerDefenseTowerArchetype TowerArchetypeAt(int index)
		{
			if (index < 0 || index >= TowerArchetypeCount)
				return null;
			return stage.TowerArchetypes[index];
		}

		public int TowerCostAt(int index)
		{
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(index);
			return archetype != null ? archetype.Cost : stage.TowerCost;
		}

		/// <summary> 코어까지 이어진 건물 수 — 검증·진단용. </summary>
		public int SuppliedBuildings => supplyChain.ConnectedCount;

		/// <summary>
		/// (배치 증분 진입점) 건설 페이즈에 타워 배치 — 자원 부족 시 즉시 false(배치 거절, 상태 무변경).
		/// 유닛데이터/프리팹 유효성은 TrySpend *전* 검증(스펙#E — 자원 뗀 뒤 스폰 실패로 자원만 날리는 것 방지).
		/// 스폰 자체는 트랩#4 준수 위해 코루틴으로 지연되지만 자원 차감은 이 호출에서 동기 확정.
		/// </summary>
		public bool TryPlaceTower(Vector3 worldPosition, int towerIndex = 0)
		{
			if (core == null || pool == null || timeManager == null || targeting == null)
				return false;
			if (stage.TowerUnit == null || stage.TowerUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.TowerUnit/Prefab 미할당 — 배치 불가(자원 미차감).");
				return false;
			}

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
			{
				// ★ 같은 자리에 같은 종류를 다시 지으면 = 승급. 별도 선택 UI 없이 「한 번 더 짓는다」는
				//   손동작 그대로라 배울 게 없고, 세로 깊이(같은 포탑을 키운다)가 생긴다.
				return TryUpgradeTowerAt(cellKey, towerIndex);
			}

			if (ValidateSite(worldPosition) == false)
				return false;
			int towerCost = CostOf(TowerDefensePlaceableKind.Tower, towerIndex);
			if (core.TrySpend(towerCost) == false)
				return Reject($"자원 부족 {core.Resource}/{towerCost}", worldPosition);

			occupiedCells.Add(cellKey);
			// 짓는 소리 — 「멀리 조용히 크는 것」과 「둥지 옆에 세우는 것」이 달라야 개척이 결정이 된다.
			EmitNoise(worldPosition, stage.NoiseFromBuild);
			// 종류가 정의돼 있으면 개척 전용 무기로, 없으면 기존 전술 경로로(하위 호환).
			TowerDefenseTowerArchetype archetype = TowerArchetypeAt(towerIndex);
			StartCoroutine(SpawnDefensiveUnitRoutine(
				stage.TowerUnit,
				archetype != null ? null : stage.TowerTactic,
				worldPosition,
				isHarvester: false,
				incomeMultiplier: 1f,
				towerArchetype: archetype));
			return true;
		}

		/// <summary> 마지막 판매의 판 값 / 실제 돌려준 액수(진단용). </summary>
		public int LastSoldValue { get; private set; }
		public int LastSellRefund { get; private set; }

		/// <summary>
		/// 그 칸에 세운 것을 판다(환불). 「실수가 되돌려지는가」 — 이게 없으면 배치가 실험이 아니라 도박이다.
		/// 코어는 못 판다(그건 자해다). 판 자리는 다시 비워져 새로 지을 수 있다.
		///
		/// ★ 파는 것은 *내가 세운 것*뿐이다 — 그 칸에 서 있는 아무 유닛이 아니라.
		///   그 칸을 지나가던 마수도 같은 칸에 서 있을 수 있는데, 예전엔 목록에서 먼저 잡히는 쪽을 팔아
		///   **마수를 공짜로 지워 없애고**(값 0) 정작 건물은 그대로 둔 채 자리만 비워졌다
		///   (실측: soldValue=0 · cellFreed=True · 건물 생존). 세운 것은 전부 보급 사슬 목록에 들어가므로
		///   그 목록이 「내 것인가」의 기준이 된다.
		/// </summary>
		public bool TrySell(Vector3 worldPosition, float refundRatio)
		{
			if (core == null || pool == null)
				return false;
			if (IsMatchOver)
				return false; // 끝난 판에선 팔 수도 없다 — 짓기와 같은 이유(끝은 끝이다).

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey) == false)
				return false;

			GameObject sold = null;
			foreach (GameObject unit in spawnedUnits)
			{
				if (unit == null || unit.activeInHierarchy == false)
					continue;
				if (ToCellKey(unit.transform.position.ToSim()) != cellKey)
					continue;
				if (coreCombatant != null && unit == coreCombatant.gameObject)
					return false; // 코어는 못 판다.
				if (supplyChain.Contains(unit.transform) == false)
					continue; // 내가 세운 것이 아니다(지나가던 마수·영웅 등) — 팔 수 없다.
				sold = unit;
				break;
			}

			if (sold == null)
				return false;

			// 판 인형은 「잃음」이 아니라고 표시해둔다 — 다음 정리에서 그 둘을 갈라 센다.
			TowerDefenseDollLabel soldDoll = FindDollLabel(sold.transform);
			if (soldDoll != null)
				soldDolls.Add(soldDoll);

			int soldValue = SoldValue(sold);
			int refund = Mathf.Max(0, Mathf.RoundToInt(soldValue * refundRatio));
			// 환불이 0 으로 나올 때 「값이 0 이라」인지 「비율이 0 이라」인지 갈라 말할 수 있게 남긴다 —
			// 이게 없으면 확인 도구가 「판매가 안 된다」까지만 말하고 이유를 못 댄다.
			LastSoldValue = soldValue;
			LastSellRefund = refund;
			core.AddResource(refund);
			PopWorldText("+" + refund, sold.transform.position.ToSim(), TextType.Heal);

			ReleaseSoldUnit(sold);
			occupiedCells.Remove(cellKey);
			return true;
		}

		// 판 값 = 그 자리에 무엇이 서 있었나. 채집이면 노드 점유도 함께 푼다(다시 잡을 수 있어야 한다).
		private int SoldValue(GameObject sold)
		{
			for (int index = harvesterTransforms.Count - 1; index >= 0; index--)
			{
				if (harvesterTransforms[index] == null || harvesterTransforms[index] != sold.transform)
					continue;

				harvesterTransforms.RemoveAt(index);
				ReleaseNodeAt(sold.transform.position.ToSim());
				return stage.HarvesterCost;
			}

			TowerDefenseWeapon weapon = sold.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
				return weapon.Cost;

			// ★ 발전 인형 — 무기도 없고 채집도 아니다. 이걸 안 갈라내면 아래 「연구」로 흘러들어가
			//   *발전기를 팔았는데 연구 단계가 깎이는*(= 모든 포탑이 약해지는) 무음 손해가 난다.
			//   전기가 끊기는 것은 팔았으니 당연하지만, 연구가 깎이는 건 아무도 시키지 않은 일이다.
			if (powerGrid.RemoveGenerator(sold.transform))
			{
				RefreshPower(); // 공급원이 사라졌으니 누가 멈추는지 즉시 다시 계산한다.
				return stage.GeneratorCost;
			}

			// ★ 여기까지 왔다 = 채집도 포탑도 발전기도 아닌 것이 내 보급 사슬에 들어 있다.
			//   예전엔 이 자리에서 「연구 인형이겠지」 하고 연구 단계를 깎았는데, 연구소가 사라진 지금
			//   그 짐작은 *아무도 시키지 않은 손해*만 남긴다. 값을 0 으로 돌리고 소리 내어 알린다 —
			//   조용히 넘어가면 다음 사람이 또 같은 짐작을 한다.
			Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 정체를 모르는 건물을 팔았다({sold.name}) — 환불 0. 새 건물 종류를 넣고 판매 값을 안 정한 것이다.");
			return 0;
		}
	}
}
