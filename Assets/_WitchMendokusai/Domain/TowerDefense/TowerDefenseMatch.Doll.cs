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
	// TowerDefenseMatch 의 인형과 건물 상태 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		private int nextDollOrdinal;

		// 부서진 자리를 알리려면 *부서지기 전* 자리를 알아야 한다 — 사라진 뒤엔 물어볼 데가 없다.
		private readonly Dictionary<Transform, Vector3> lastBuildingPositions = new();

		/// <summary>
		/// 내 것이 부서졌으면 그 자리를 알린다.
		///
		/// ★ 부서진 *뒤에는* 자리를 물어볼 데가 없다(참조가 비어 버린다). 그래서 살아 있는 동안
		///   마지막 자리를 계속 적어 둔다 — 이게 없으면 「어딘가 부서졌다」까지만 알고 어디인지 모른다.
		/// ★ 이 장르에서 사람들이 가장 많이 꼽는 불만이 「무슨 일이 났는지 안 알려준다」였다.
		///   화면 밖 한 곳이 뚫리는 것을 못 보면, 알아챘을 땐 이미 늦는다.
		/// </summary>
		private void TrackLostBuildings()
		{
			if (stage == null)
				return;

			alerts.Prune(Time.time);
			breach.Tick(Time.deltaTime, stage.BreachCoolPerSecond); // 한 번 실수가 영원한 벌이 되면 안 된다.
			noise.Tick(Time.deltaTime, stage.NoiseDecayPerSecond); // 소리는 잦아든다 — 조용해질 기회가 있어야 한다.

			foreach (Transform building in supplyChain.Buildings)
			{
				if (building != null)
					lastBuildingPositions[building] = building.position.ToSim();
			}

			List<Transform> lost = null;
			foreach (KeyValuePair<Transform, Vector3> tracked in lastBuildingPositions)
			{
				if (tracked.Key != null)
					continue;

				alerts.Raise("내 것이 부서졌다", tracked.Value, Time.time, stage.AlertSeconds);
				// 부서진 자리는 잊히지 않는다 — 다음 파도가 이쪽으로 끌린다.
				// ★ 그리고 그걸 *말해 준다*. 방향만 조용히 바꾸면 사람은 「이번엔 왜 여기로 오지」만
				//   남고 자기 선택과 결과를 못 잇는다 — 안 보이는 규칙은 없는 규칙이다.
				//   처음 뜨거워지는 순간 딱 한 번만 외친다(잃을 때마다 외치면 급한 알림을 덮는다).
				// 무너지는 소리가 가장 크다 — 이게 소리 사태의 시작점이다.
				EmitNoise(tracked.Value, stage.NoiseFromLoss);
				if (breach.Add(tracked.Value, stage.BreachMergeDistance, stage.BreachHeatPerLoss))
					alerts.Raise("뚫린 곳을 다시 노린다", tracked.Value, Time.time, stage.AlertSeconds);
				lost ??= new List<Transform>();
				lost.Add(tracked.Key);
			}

			if (lost == null)
				return;
			foreach (Transform gone in lost)
				lastBuildingPositions.Remove(gone);
		}

		public bool DestroyFarthestBuildingForVerification(out Vector3 destroyedAt)
		{
			destroyedAt = Vector3.zero;
			if (coreCombatant == null)
				return false;

			Transform farthest = null;
			float bestDistance = -1f;
			foreach (Transform building in supplyChain.Buildings)
			{
				if (building == null || building == coreCombatant.transform)
					continue;

				float distance = Vector3.Distance(building.position.ToSim(), coreCombatant.Position);
				if (distance <= bestDistance)
					continue;
				bestDistance = distance;
				farthest = building;
			}

			// ★ 코어 위(또는 코앞)에 있는 것을 고르면 잃은 방향이 0 도로 나와 「끌렸나」를 못 가른다.
			//   실제로 전체 실행에서 그렇게 뽑혀 「잃은 쪽 0.0도 · 뜨거운 자리 0곳」이라는 읽을 수
			//   없는 결과가 나왔다. 방향이 성립할 만큼 떨어진 것이 없으면 **없앨 것이 없다**고 답한다
			//   — 아무거나 없애고 재는 것보다 「못 쟀다」가 낫다.
			if (farthest == null || bestDistance < MIN_VERIFY_LOSS_DISTANCE)
				return false;

			destroyedAt = farthest.position.ToSim();
			Destroy(farthest.gameObject);
			return true;
		}

		/// <summary> 세워진 인형에게 이름을 준다 + 한 마디 시킨다. 같은 판·같은 순서면 같은 이름. </summary>
		private void RegisterDoll(Transform anchor, Color tint, bool isHarvester = false,
			bool isPlacedBuilding = false, int variant = 0)
		{
			if (anchor == null)
				return;

			int ordinal = nextDollOrdinal++;
			string name = TowerDefenseNames.For(MapSeed, ordinal);
			TowerDefenseDollLabel doll = new(anchor, name, tint,
				stage.BuildingLevelBaseCost, stage.BuildingLevelGrowth)
			{
				BuildingId = MapSeed + ordinal * 7919,
				IsHarvester = isHarvester,
				IsPlacedBuilding = isPlacedBuilding,
				Variant = variant,
			};
			dollLabels.Add(doll);
			PopWorldText("「" + name + "」 " + TowerDefenseNames.Greeting(MapSeed, ordinal), anchor.position.ToSim(), TextType.Heal);
		}

		/// <summary>
		/// 건물마다 「지금 얼마나 찼나 / 일하고 있나」를 이름표에 채워 넣는다.
		/// 화면이 유닛에게 직접 캐물으면 표시와 규칙이 두 경로로 갈라지므로, 규칙을 아는 쪽이 채운다.
		/// </summary>
		private void RefreshBuildingProgress()
		{
			foreach (TowerDefenseDollLabel label in dollLabels)
			{
				if (label.IsAlive == false)
					continue;

				bool powered = IsPowered(label.Anchor);
				TowerDefenseWeapon weapon = label.Anchor.GetComponent<TowerDefenseWeapon>();
				if (weapon != null)
				{
					label.ReadyRatio = weapon.ReadyRatio;
					label.Working = powered;
					continue;
				}

				if (harvesterTransforms.Contains(label.Anchor))
				{
					// 채집은 「다음 정산까지」가 곧 진행이다 — 시계가 돌면 들어온다.
					// ★ 단, *일하고 있을 때만* 찬다. 멈춘 인형의 바가 계속 차오르면 화면이 거짓말을 한다
					//   (사용자 실증: "전기 없다고 뜨는데 채굴은 또 되는 것 같고"). 규칙은 이미 한 푼도
					//   안 주고 있었으므로, 갈라진 것은 그림뿐이었다 — 안 도는 것은 안 차야 한다.
					bool working = powered && label.Disconnected == false;
					label.ReadyRatio = working && core != null && stage.Rules.IncomeInterval > 0f
						? 1f - core.NextIncomeIn / stage.Rules.IncomeInterval
						: 0f;
					label.Working = working;
					continue;
				}

				label.ReadyRatio = 1f; // 패시브 — 언제나 준비됨.
				label.Working = powered;
			}
		}

		/// <summary> 판이 끝난 뒤 화면이 그대로 읽는 한 덩어리 요약. </summary>
		public string BuildSummary()
		{
			string newline = System.Environment.NewLine;
			// 씨앗을 적어둔다 — 끝난 직후가 「이 판 해봐」를 건네기 가장 자연스러운 순간이다.
			return "씨앗 " + MapSeed + newline
				+ "지음 " + BuiltCount + "  ·  잃음 " + LostCount + newline
				+ "잡음 " + KilledCount + "  ·  샌 마수 " + LeakedCount + newline
				+ "한때 " + PeakEnemies + "마리까지  ·  마수 강도 x" + Pressure.ToString("0.0");
		}

		/// <summary> 고른 건물의 성장 정보(없으면 null) — 화면이 선택지를 그릴 때 쓴다. </summary>
		public TowerDefenseDollLabel FindDoll(MatchCombatant combatant)
		{
			return combatant != null ? FindDollLabel(combatant.transform) : null;
		}

		/// <summary> 고른 건물의 레벨업 선택지를 확정한다. </summary>
		public bool ChooseBuildingPerk(MatchCombatant combatant, TowerDefenseBuildingPerk perk)
		{
			TowerDefenseDollLabel doll = FindDoll(combatant);
			if (doll == null || doll.Progress.Choose(perk) == false)
				return false;

			ApplyPerk(doll, perk);
			PopWorldText(TowerDefenseBuildingProgress.NameOf(perk), doll.Anchor.position.ToSim(), TextType.Exp);
			return true;
		}
	}
}
