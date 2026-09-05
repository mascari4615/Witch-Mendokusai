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
	// TowerDefenseMatch 의 Save 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		/// <summary>
		/// 이어하기 복원이 도는 중인가 — 끝나기 전에 「건물 수가 다르다」를 재면 *멀쩡한 복원*을 결함으로 잡는다.
		/// (실측: 복원이 한 프레임씩 양보하며 도는 동안 하네스가 중간값을 읽어 거짓 실패를 냈다.)
		/// </summary>
		public bool RestoreInProgress { get; private set; }

		/// <summary> 지금 판을 저장 가능한 형태로 뽑는다(끝난 판이면 null). </summary>
		public TowerDefenseSaveData CaptureSave()
		{
			if (core == null || stage == null || core.Outcome != TowerDefenseOutcome.InProgress)
				return null;

			TowerDefenseSaveData save = new()
			{
				StageId = stage.ID.ToString(),
				MapSeed = MapSeed,
				MapWidth = mapLayout != null ? mapLayout.Width : 0,
				MapLength = mapLayout != null ? mapLayout.Length : 0,
				Difficulty = (int)Difficulty,
				ElapsedSeconds = core.ElapsedSeconds,
				WaveIndex = core.WaveIndex,
				Resource = core.Resource,
				Essence = core.Essence,
				Lives = core.Lives,
				CoreLevel = CoreLevel,
				CoreExperience = coreProgress.Experience,
				CorePendingChoices = CorePendingChoices,
				ResearchLevel = LabCount,
				NestsDestroyed = NestsDestroyed,
			};

			// 이 판의 성격 — 고른 카드와 부순 둥지. 이게 빠지면 이어한 판이 「같은 판」이 아니다.
			foreach (TowerDefenseBoonKind kind in boons.TakenKinds)
				save.TakenBoons.Add((int)kind);
			// 성좌 자국 — 정본은 화면이 들고 있으므로 물어서 받아 적는다(값을 두 곳에 두지 않는다).
			CollectResearchInto(save.TakenResearch);
			save.DestroyedNestPositions.AddRange(destroyedNestPositions);

			foreach (TowerDefenseDollLabel doll in dollLabels)
			{
				// 사람이 세운 것만 적는다 — 영웅처럼 판이 스스로 만드는 인형까지 적으면 이어할 때마다 는다.
				if (doll.IsAlive == false || doll.IsPlacedBuilding == false)
					continue;

				TowerDefenseBuildingSave building = new()
				{
					Kind = (int)(doll.IsHarvester ? TowerDefensePlaceableKind.Harvester : TowerDefensePlaceableKind.Tower),
					Variant = doll.Variant,
					Position = stageRoot.InverseTransformPoint(doll.Anchor.position).ToSim(),
					Level = doll.Level,
					Experience = doll.Progress.Experience,
					PendingChoices = doll.Progress.PendingChoices,
					Perks = new List<int>(),
				};
				foreach (TowerDefenseBuildingPerk perk in doll.Progress.Taken)
					building.Perks.Add((int)perk);

				save.Buildings.Add(building);
			}

			// 함정도 적는다 — 자리와 남은 횟수를 같이. 안 적으면 깔아둔 함정이 이어하는 순간 통째로 사라진다.
			if (stageRoot != null)
			{
				foreach (TowerDefenseTrap trap in stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true))
				{
					save.Traps.Add(new TowerDefenseTrapSave
					{
						Position = stageRoot.InverseTransformPoint(trap.transform.position).ToSim(),
						ChargesLeft = trap.ChargesLeft,
					});
				}
			}

			// 전초기지도 적는다 — 이건 보급의 *새 원점*이라 안 적으면 그 일대가 통째로 사슬 밖이 된다.
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					save.Outposts.Add(stageRoot.InverseTransformPoint(outpost.position).ToSim());
			}

			// 벽도 적는다 — 벽은 보급 징검다리라, 안 적으면 사슬이 짧아져 그 너머 포탑이 되살아나지 못한다.
			foreach (Vector2Int wallCell in wallCells)
				save.Walls.Add(mapLayout != null ? mapLayout.CellToWorld(wallCell) : Vector3.zero);

			return save;
		}

		/// <summary>
		/// 저장을 이어받아 판을 그 상태로 되돌린다 — **Begin 직전**에 부른다.
		///
		/// ★ 왜 직전인가: 지형이 같아야 「이어하기」다. 판은 씨앗에서 태어나므로 씨앗을 먼저 넘겨야
		///   같은 땅이 다시 나온다. Begin 뒤에 부르면 이미 다른 땅이 깔린 뒤라 내 건물만 엉뚱한 자리에
		///   다시 서게 된다. 그래서 여기서는 예약만 하고, 판을 깔 때 씨앗을, 판이 선 뒤 건물을 얹는다.
		/// </summary>
		public void RestoreSave(TowerDefenseSaveData save)
		{
			if (save == null || save.IsResumable == false)
				return;

			pendingRestore = save;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 이어하기 — {save.Describe()}");
		}

		private TowerDefenseSaveData pendingRestore;

		/// <summary>
		/// 실제 복원 — 건물 스폰이 코루틴이라 Begin 이 끝난 *다음*에 한 채씩 다시 세운다.
		/// 값(자원·정수·목숨)은 먼저 맞춰야 세우는 도중에 「돈이 없어 거절」이 나지 않는다.
		/// </summary>
		private IEnumerator RestoreRoutine(TowerDefenseSaveData save)
		{
			core.AddResource(Mathf.Max(0, save.Resource - core.Resource));
			core.AddEssence(Mathf.Max(0, save.Essence - core.Essence));
			LabCount = save.ResearchLevel;
			RefreshAvailableSlots(); // 이어하면 그때 열려 있던 칸이 그대로 서야 한다.
			NestsDestroyed = save.NestsDestroyed;

			// 판의 시계·목숨·코어 성장 — 이게 안 돌아오면 오래 버틴 판이 이어하는 순간 처음으로 되감긴다.
			core.Restore(save.ElapsedSeconds, save.WaveIndex, save.Lives);
			coreProgress.Restore(save.CoreLevel, save.CoreExperience, save.CorePendingChoices, null);

			// 고른 카드 — 종류만 적어뒀고 값은 이 판의 규칙에서 다시 나온다(같은 규칙 = 같은 값).
			// 즉시 효과(목숨·정수·자원)는 다시 주지 않는다 — 그 결과는 위에서 이미 되돌렸다.
			// 성좌 — 화면에 자국을 되돌리고, 효과도 같이 다시 쌓는다(둘 중 하나만 하면 갈라진다).
			if (save.TakenResearch != null && save.TakenResearch.Count > 0)
				RestoreResearchFrom(save.TakenResearch);

			if (save.TakenBoons != null)
			{
				foreach (int kind in save.TakenBoons)
					boons.Take(TowerDefenseDraft.Make((TowerDefenseBoonKind)kind, stage.DraftRules));
				core.IncomeMultiplier = boons.IncomeMultiplier * (1f + ResearchBonus(TowerDefenseResearchEffect.HarvestYield));
			}

			RestoreInProgress = true;

			// ★ 전초기지가 가장 먼저다 — 보급의 원점이라, 이게 서야 그 일대의 자리가 열린다.
			if (save.Outposts != null && stageRoot != null)
			{
				foreach (Vector3 outpostLocal in save.Outposts)
				{
					core.AddEssence(stage.OutpostEssenceCost); // 되살리는 것은 짓는 일이 아니다.
					if (TryPlaceOutpost(stageRoot.TransformPoint(outpostLocal.ToUnity()).ToSim()) == false)
						core.TrySpendEssence(stage.OutpostEssenceCost);
					yield return null;
				}
			}

			// ★ 벽을 **먼저** 세운다 — 벽이 보급을 뻗어 주므로, 뒤에 놓을 포탑의 자리가 그때 열린다.
			if (save.Walls != null && stageRoot != null)
			{
				int wallsBack = 0;
				foreach (Vector3 wallLocal in save.Walls)
				{
					core.AddResource(stage.WallCost); // 되살리는 것은 짓는 일이 아니다 — 값은 아래에서 정확히 맞춘다.
					if (TryPlaceWall(stageRoot.TransformPoint(wallLocal.ToUnity()).ToSim()))
						wallsBack++;
					else
						core.TrySpend(stage.WallCost);
					yield return null;
				}
				if (wallsBack < save.Walls.Count)
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이어하기 — 벽 {save.Walls.Count - wallsBack}칸을 못 되살렸다.");
			}

			// 함정은 보급 사슬과 무관하다 — 자리만 맞으면 서므로 한 번에 되돌린다.
			if (save.Traps != null && stageRoot != null)
			{
				foreach (TowerDefenseTrapSave trapSave in save.Traps)
				{
					Vector3 trapWorld = stageRoot.TransformPoint(trapSave.Position.ToUnity()).ToSim();
					core.AddResource(stage.TrapCost); // 되살리는 것은 짓는 일이 아니다.
					if (TryPlaceTrap(trapWorld) == false)
					{
						core.TrySpend(stage.TrapCost);
						continue;
					}

					// ★ 남은 횟수를 도로 얹는다 — 안 하면 다 쓴 함정이 새것으로 살아나 「닳는다」가 무효가 된다.
					foreach (TowerDefenseTrap trap in stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true))
					{
						if ((trap.transform.position.ToSim() - trapWorld).sqrMagnitude <= 1f)
						{
							trap.RestoreCharges(trapSave.ChargesLeft);
							break;
						}
					}
					yield return null;
				}
			}

			// ★ **순서에 기대지 않는다.** 지을 수 있는 자리는 「보급이 닿는 곳」이고, 보급은 내 건물이
			//   징검다리라 *다른 건물이 먼저 서야* 뻗어 나간다. 저장 순서대로 한 번만 훑으면 바깥 것이
			//   「보급이 안 닿는다」로 거절되고 그대로 사라진다 — 라이브 실측에서 9채가 3채로 줄었다.
			//   그래서 **놓을 수 있는 것을 놓고, 놓은 게 있으면 다시 훑는다.** 더 못 놓으면 멈춘다.
			List<TowerDefenseBuildingSave> pending = new(save.Buildings);
			List<TowerDefenseBuildingSave> stillPending = new();

			while (pending.Count > 0)
			{
				stillPending.Clear();
				int placedThisPass = 0;

				foreach (TowerDefenseBuildingSave building in pending)
				{
					Vector3 world = stageRoot.TransformPoint(building.Position.ToUnity()).ToSim();
					// ★ 되살리는 것은 *짓는 일이 아니다* — 이미 치른 값을 또 치르면 이어할 때마다 지갑이 깎인다.
					//   배치 경로를 그대로 쓰되(자리·보급 규칙은 지켜야 한다) 그 값만큼 미리 채워 넣고,
					//   전부 세운 뒤 저장된 액수로 정확히 되돌린다.
					int restoreCost = building.Kind == (int)TowerDefensePlaceableKind.Harvester
						? stage.HarvesterCost
						: TowerCostAt(building.Variant);
					core.AddResource(restoreCost);

					bool placed = building.Kind == (int)TowerDefensePlaceableKind.Harvester
						? TryPlaceHarvester(world)
						: TryPlaceTower(world, building.Variant);

					if (placed == false)
					{
						// 아직 못 놓는다 = *지금은* 보급이 안 닿는다. 다음 통과에서 다시 본다.
						// 미리 채운 값은 도로 뺀다 — 안 그러면 통과할 때마다 지갑이 부풀어 오른다.
						core.TrySpend(restoreCost);
						stillPending.Add(building);
						continue;
					}

					placedThisPass++;

					yield return null; // 스폰이 끝나야 그 인형에 성장을 얹을 수 있다.
					yield return null;

					TowerDefenseDollLabel doll = FindDollLabel(world);
					if (doll == null)
						continue;

					// 고른 것들은 *효과*를 다시 얹어야 한다(수치가 붙는 일이라 기록만으론 부족).
					if (building.Perks != null)
					{
						foreach (int perk in building.Perks)
							ApplyPerk(doll, (TowerDefenseBuildingPerk)perk);
					}

					// 자란 단계·경험치는 기록 그대로 얹는다 — 경험치로 되감으면 선택지가 다시 쌓인다.
					doll.Progress.Restore(building.Level, building.Experience, building.PendingChoices, doll.Progress.Taken);
					doll.Level = building.Level;

					// 승급은 무기에도 걸려 있다 — 이름표만 올리면 사거리·피해가 1단계인 채로 남는다.
					TowerDefenseWeapon weapon = doll.Anchor != null ? doll.Anchor.GetComponent<TowerDefenseWeapon>() : null;
					if (weapon == null)
						continue;
					while (weapon.Level < building.Level && weapon.TryUpgrade())
					{
					}
					RefreshTowerRing(weapon.gameObject);
				}

				// 한 바퀴 돌았는데 하나도 못 놓았으면 더 돌아도 결과가 같다 — 그 자리들은 진짜로 못 놓는다.
				if (placedThisPass == 0)
				{
					if (stillPending.Count > 0)
					{
						Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이어하기 — {stillPending.Count}채를 되살릴 자리가 없다"
							+ " (지형이 바뀌었거나 보급이 끊긴 자리).");
					}
					break;
				}

				pending.Clear();
				pending.AddRange(stillPending);
			}

			// 지갑을 저장된 액수로 정확히 맞춘다 — 남거나 모자라면 이어할 때마다 판이 조금씩 달라진다.
			core.AddResource(Mathf.Max(0, save.Resource - core.Resource));
			if (core.Resource > save.Resource)
				core.TrySpend(core.Resource - save.Resource);
			core.AddEssence(Mathf.Max(0, save.Essence - core.Essence));
			if (core.Essence > save.Essence)
				core.TrySpendEssence(core.Essence - save.Essence);

			RestoreInProgress = false;

			Debug.Log($"{nameof(TowerDefenseMatch)}: 이어하기 복원 끝 — 건물 {dollLabels.Count}채"
				+ $" · 자원 {core.Resource}/{save.Resource} · 정수 {core.Essence}/{save.Essence}.");
		}

		/// <summary> 모드를 나가거나 매치가 끝나면 반드시 원래 속도로 — 안 되돌리면 본편이 멈춘 채 남는다. </summary>
		public void RestoreTimeScale()
		{
			Time.timeScale = 1f;
		}

		/// <summary> 이어할 때 「이 마디들을 다시 찍은 것으로 하라」는 신호. </summary>
		public event System.Action<List<int>> RestoreResearch = delegate { };

		public void RestoreResearchFrom(List<int> ids) => RestoreResearch(ids);
	}
}
