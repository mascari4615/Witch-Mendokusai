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
	// TowerDefenseMatch 의 핵 성장과 은혜 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 고른 카드가 쌓이는 곳 — 코어 레벨업 선택이 여기로 들어온다.
		// 카드가 걸려 있는 동안 진행이 멈춘다 = 「강제 선택」의 실체.
		private readonly TowerDefenseBoonState boons = new();

		// 이름 붙은 인형들 — 화면이 이름표를 띄우는 데 필요한 최소 정보.
		private readonly List<TowerDefenseDollLabel> dollLabels = new();

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
	}
}
