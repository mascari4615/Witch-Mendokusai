using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 짓기 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static bool towerPlanFlipped;
		// 판매 검증용 — 배치 좌표. 스폰은 코루틴(1프레임 양보)이라 *같은 틱에 팔면 아직 아무도 없다*.
		private static Vector3 sellProbeLocal;
		private static int sellProbeSlot;
		private static bool sellProbeReady;
		private static readonly List<int> nodeOrder = new();

		private static void DoPlacements()
		{
			Transform stageRoot = FindStageRoot();
			if (stageRoot == null)
			{
				Debug.LogError(TAG + " PLACE-FAIL StageRoot 없음");
				return;
			}
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
			{
				Debug.LogError(TAG + " PLACE-FAIL controller 없음");
				return;
			}

			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			// 화면 좌표 계산은 **실제 렌더 카메라** 기준이어야 한다 — 개척이 정식 content 카메라가 되면서
			// 렌더 카메라는 Cinemachine brain 이 물고 있는 단 하나다(전용 Camera 자식은 폐기됨).
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null)
			{
				Debug.LogError(TAG + " PLACE-FAIL placement=" + (placement != null) + " renderCamera=" + (modeCamera != null));
				return;
			}
			Debug.Log(TAG + " PLACE-VIA-SCREEN renderCamera=" + modeCamera.name + " pos=" + modeCamera.transform.position);

			int before = match.Resource;

			// 방어인형 — 판이 매 매치 새로 생성되므로 고정 좌표는 암반 위일 수 있다(그러면 배치가 조용히
			// 전부 거절돼 "방어 없는 판"을 방어 있는 판으로 착각한다). 코어 주변에서 *실제로 설 수 있는* 칸을 찾는다.
			// 종류를 섞어 세운다 — 한 종류만 세우면 광역·관통·둔화가 통째로 미검증으로 남는다.
			// 두 번의 배치(최초/재시작)에서 서로 다른 두 쌍을 세운다 — 한 쌍만 세우면 나머지 두 종류의
			// 효과가 통째로 미검증으로 남는다. 예산 160 안에서 각각 성립하는 조합.
			// 관측 구간(재시작 뒤)에 서 있는 쪽이 검증 대상이다 — 첫 판에 세운 포탑은 재시작이 치운다.
			// 그래서 *두 번째* 조합에 아직 미확인인 종류를 넣는다(관통은 직전 실행에서 확인됨).
			// ★ 확인할 항목(승급·함정·벽·연구)이 여럿인데 예산은 유한하다 — 초기 배치가 다 쓰면
			//   나머지가 전부 「돈이 없어 못 함」으로 끝나 *기능이 아니라 잔고*를 검사하게 된다.
			//   그래서 초기엔 한 기만 세우고 남은 예산을 항목들이 나눠 쓴다.
			int[] slotPlan = towerPlanFlipped ? new[] { 1 } : new[] { 0 };
			towerPlanFlipped = towerPlanFlipped == false;
			int towersPlaced = 0;
			List<Vector3> spots = FindPlaceableSpots(stageRoot, slotPlan.Length);
			for (int index = 0; index < spots.Count; index++)
			{
				int slot = slotPlan[index % slotPlan.Length];
				placement.SelectSlot(slot);
				int beforeTower = match.Resource;
				placement.PlaceTowerAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(spots[index].ToUnity()).ToSim()));
				if (match.Resource < beforeTower)
					towersPlaced++;
			}
			// 시간 조작 — 멈춤·배속이 실제로 걸리는가(화면 버튼과 같은 경로).
			match.TogglePause();
			float paused = match.SpeedScale;
			match.TogglePause();
			match.CycleSpeed();
			Debug.Log(TAG + " TIME paused=" + paused.ToString("F0") + " cycled=" + match.SpeedScale.ToString("F0")
				+ " timeScale=" + Time.timeScale.ToString("F0"));
			match.CycleSpeed(); match.CycleSpeed(); // ×1 로 되돌림(순환).

			placement.SelectSlot(match.TowerArchetypeCount); // 채집 칸으로 되돌림.
			if (spots.Count > 0)
			{
				sellProbeLocal = spots[0];
				// ★ *어떤 종류를 세웠는지*도 같이 적어둔다 — 판마다 종류를 번갈아 세우면서 승급은 늘 0번으로
				//   걸고 있었다. 그러면 다른 종류라 규칙대로 거절되는데 하네스는 「승급이 안 된다」고 적는다.
				sellProbeSlot = slotPlan[0];
				sellProbeReady = true;
			}
			Debug.Log(TAG + " PLACE-TOWERS placed=" + towersPlaced
				+ " towerKinds=" + match.TowerArchetypeCount);

			// 채집인형 = 자원 노드 위. 좌표는 **스테이지 정본에서 읽는다** — 하네스에 박아두면 노드를
			// 옮기는 순간 "노드 위 배치" 검사가 조용히 "빈 땅 배치(항상 거절)" 로 바뀌어 무의미해진다.
			// ★ 절차 생성이면 노드가 매 판 다르다 — 스테이지 SO 의 고정 좌표를 읽으면 항상 빈 땅을 찍는다.
			IReadOnlyList<Vector3> nodeLocals = match.ActiveResourceNodeLocalPositions;
			if (nodeLocals.Count == 0)
				Debug.LogError(TAG + " PLACE-FAIL 스테이지에 자원 노드가 없음");
			// ★ 노드 전부에 세우면(6곳 × 60) 예산이 통째로 사라져 뒤의 확인이 전부 「돈이 없어 못 함」이 된다.
			//   여기서 볼 것은 「노드 위에 서는가」이므로 한 기면 충분하다.
			// ★ 채집 스폰은 코루틴(1프레임 양보 후 수입 반영)이라 *세운 그 틱에 읽으면 0*이다.
			//   그래서 확인(VerifyEssence)이 아니라 여기서 미리 세운다 — 1.5초 뒤에 읽힌다.
			//   바깥 노드(배수 큰 곳)를 우선 — 정수는 거기서만 난다.
			// ★ 돈이 모자라 못 세운 것을 「규칙이 막았다」와 구분할 수 없다 — 실측에서 둘째 채집이 늘
			//   거절돼 정수 경로가 매번 「확인 못 함」으로 끝났다. 값만 채워두고, *배치 규칙은 그대로* 둔다
			//   (보급·암반·점유는 안 건드린다 — 그걸 우회하면 확인 자체가 거짓이 된다).
			// 정수도 채운다 — 바깥 노드는 *전초기지로 보급을 늘려야* 닿는데 그게 정수를 쓴다.
			// 판에서는 둥지를 부수면 정수가 나므로 막힌 설계가 아니지만, 하네스는 그 시간을 못 기다린다.
			match.GrantForVerification(2000, 200);

			int harvestersPlaced = 0;
			nodeOrder.Clear();
			for (int index = 0; index < nodeLocals.Count; index++)
				nodeOrder.Add(index);
			// ★ 이제 「보급이 닿는 곳에만」 지을 수 있다 — 먼 노드부터 노리면 전부 거절돼 채집이 0 이 되고,
			//   그러면 정수·보급 확인이 통째로 무의미해진다(라이브 실증: 거절 로그만 쌓였다).
			//   사람이 하는 순서대로 *코어에서 가까운 것부터* 잡는다.
			Vector3 coreLocal = stageRoot.InverseTransformPoint(match.CoreCombatant != null
				? match.CoreCombatant.Position.ToUnity()
				: stageRoot.position).ToSim();
			nodeOrder.Sort((left, right) =>
				(nodeLocals[left] - coreLocal).sqrMagnitude.CompareTo((nodeLocals[right] - coreLocal).sqrMagnitude));

			Vector3 firstHarvesterLocal = Vector3.zero;
			foreach (int nodeIndex in nodeOrder)
			{
				Vector3 local = nodeLocals[nodeIndex];
				// ★ 가까운 것 하나 + 그보다 먼 것 하나 = 「이어지는가」와 「바깥에서 정수가 나오는가」를
				//   한 판에서 같이 본다. 가까운 것만 잡으면 정수 확인이 영영 「바깥에 세운 게 없음」으로만 끝난다.
				if (harvestersPlaced >= 2)
					break;
				// 둘째는 첫째와 충분히 떨어진 곳으로 — 붙여 세우면 같은 광맥을 물어 바깥 확인이 안 된다.
				if (harvestersPlaced == 1 && (nodeLocals[nodeIndex] - firstHarvesterLocal).sqrMagnitude < 400f)
					continue;
				int beforeHarvester = match.Resource;
				placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(local.ToUnity()).ToSim()));
				if (match.Resource < beforeHarvester)
				{
					if (harvestersPlaced == 0)
						firstHarvesterLocal = local;
					harvestersPlaced++;
				}
			}

			// ★ 바깥으로 한 걸음 — 전초기지가 새 보급 원점이 된다. 이걸 안 하면 먼 노드는 영영 거절돼
			//   「바깥 채집이 정수를 내는가」가 매 판 확인 못 함으로 끝난다(실측: 여덟 판 내리 그랬다).
			if (harvestersPlaced > 0 && match.Essence >= match.Stage.OutpostEssenceCost)
			{
				// ★ 「멀다」로 고르면 안 된다 — 거리로 여덟 판을 골랐는데 전부 안쪽 등급이었다(판에 바깥
				//   광맥이 25개나 있는데도). 등급을 판에 직접 묻는다.
				List<Vector3> outerNodes = new List<Vector3>();
				match.CollectOuterNodeLocalPositions(outerNodes);
				if (outerNodes.Count == 0)
				{
					Debug.LogError(TAG + " OUTER-HARVEST-FAIL 판에 바깥 광맥이 없다.");
				}
				else
				{
					// ★ 광맥 좌표는 *무대 기준*이다 — 코어의 월드 좌표와 그냥 빼면 거리가 1900 이 나온다
					//   (지도는 250 남짓인데). 한 공간으로 맞춰서 잰다.
					outerNodes.Sort((left, right) =>
						(left - coreLocal).sqrMagnitude.CompareTo((right - coreLocal).sqrMagnitude));
					Vector3 targetLocal = outerNodes[0]; // 가장 가까운 바깥 광맥 — 사람도 여기부터 뻗는다.

					// 보급 원점을 그 광맥 쪽으로 한 걸음씩 — 한 번에 못 닿으면 여러 걸음 놓는다.
					for (int step = 1; step <= 4 && match.Essence >= match.Stage.OutpostEssenceCost; step++)
					{
						Vector3 towardLocal = Vector3.Lerp(coreLocal, targetLocal, step / 5f);
						match.TryPlaceOutpost(stageRoot.TransformPoint(towardLocal.ToUnity()).ToSim());
					}

					Debug.Log(TAG + " OUTPOST-REACH 바깥 광맥까지 보급 뻗기 · 전초기지 " + match.OutpostCount
						+ "개 · 목표거리 " + (targetLocal - coreLocal).magnitude.ToString("F1")
						+ " · 보급거리 " + match.EffectiveSupplyReach.ToString("F1"));

					int beforeFar = match.Resource;
					placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(targetLocal.ToUnity()).ToSim()));
					bool outerPlaced = match.Resource < beforeFar;
					Debug.Log(TAG + " OUTER-HARVEST 바깥 광맥 채집 "
						+ (outerPlaced ? "성공" : "거절됨(보급 미도달)"));

					// ★ 보급과 전기는 *다른 관문*이다 — 이어져도 전기가 안 닿으면 정수는 0 이다.
					//   여기까지 안 놓으면 확인이 늘 「이어졌지만 전기가 안 닿음」에서 멈춘다(실측).
					if (outerPlaced)
					{
						int beforeGenerator = match.Resource;
						Vector3 besideNode = targetLocal + new Vector3(match.Stage.GeneratorRadius * 0.4f, 0f, 0f);
						match.TryPlaceGenerator(stageRoot.TransformPoint(besideNode.ToUnity()).ToSim());
						if (match.Resource == beforeGenerator)
							match.TryPlaceGenerator(stageRoot.TransformPoint(
								(targetLocal - new Vector3(match.Stage.GeneratorRadius * 0.4f, 0f, 0f)).ToUnity()).ToSim());
						Debug.Log(TAG + " OUTER-POWER 바깥 채집 옆에 발전 인형 "
							+ (match.Resource < beforeGenerator ? "세움" : "못 세움")
							+ " · 전기 반경 " + match.Stage.GeneratorRadius.ToString("F1"));
					}
				}
			}

			// ★ 징검다리 — 먼 노드는 코어에서 한 번에 안 닿는다. 사람이 하는 일(중간에 하나 세워 잇기)을
			//   하네스도 해야 「사슬이 실제로 도는가」를 볼 수 있다. 안 하면 늘 「끊김」만 보고 규칙이
			//   고장난 줄 알게 된다(실측: 정수 0 이 계속 나왔는데 원인은 사슬 미구축이었다).
			if (harvestersPlaced > 0)
			{
				placement.SelectSlot(0);
				Vector3 bridgeLocal = firstHarvesterLocal * 0.5f; // 코어(원점)와 노드의 중간.
				placement.PlaceTowerAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(bridgeLocal.ToUnity()).ToSim()));
				Debug.Log(TAG + " SUPPLY-BRIDGE 중간에 하나 세워 사슬 시도 local=" + bridgeLocal);
			}

			// 노드에서 먼 빈 땅에 채집 시도 = 거절돼야 정상(노드 결합 규칙 살아있음 확인).
			int beforeOffNode = match.Resource;
			placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(new Vector3(0f, 0f, 2f).ToUnity()).ToSim()));
			bool offNodeRejected = match.Resource == beforeOffNode;

			Debug.Log(TAG + " PLACE resourceBefore=" + before + " after=" + match.Resource
				+ " offNodeHarvesterRejected=" + offNodeRejected);


			VerifyHeroAndNames(stageRoot);

			LogHudState();
			LogNodeMarkers(stageRoot);
		}

		private static void VerifyUpgrade()
		{
			Transform stageRoot = FindStageRoot();
			if (sellProbeReady == false || match == null || stageRoot == null)
				return;

			// ★ 새로 짓지 않고 *이미 세운* 포탑을 올린다 — 확인하려는 건 「같은 자리에 다시 지으면 자라는가」이지
			//   「지을 돈이 있는가」가 아니다. 새로 지으면 그 값이 승급 예산을 먹어 기능이 아니라 잔고를 검사하게 된다.
			Vector3 world = stageRoot.TransformPoint(sellProbeLocal.ToUnity()).ToSim();

			// ★ 승급은 정수(강화 전용 재화)를 쓴다 — 정수는 웨이브 정산에서만 나오므로 첫 웨이브 전에는 못 올린다.
			//   이건 의도된 설계(강화는 개척의 결과)라, 없으면 「확인 못 함」이지 실패가 아니다.
			if (match.Essence <= 0)
			{
				Debug.Log(TAG + " UPGRADE-SKIP 정수 0 — 첫 웨이브 정산 전에는 승급 불가(의도된 설계)");
				return;
			}

			int before = match.Essence;
			bool upgraded = match.TryPlaceTower(world, sellProbeSlot); // 세운 그 종류로 걸어야 승급 검사가 된다.

			int level = -1;
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include))
			{
				if (weapon != null && weapon.Level > level)
					level = weapon.Level;
			}

			string verdict = TAG + " UPGRADE ok=" + upgraded + " slot=" + sellProbeSlot + " maxLevel=" + level
				+ " essence " + before + " → " + match.Essence;
			if (upgraded && level >= 2 && match.Essence < before)
				Debug.Log(verdict + " → 같은 자리에 다시 지으면 자란다 ✔");
			else
				Debug.LogError(verdict + " → 승급이 안 되거나 값을 안 치른다.");
		}

		/// <summary>
		/// 확인 항목에 *그 항목이 쓸 몫*을 채워준다 — 앞선 배치가 예산을 다 쓰면 뒤의 확인이 전부
		/// 「돈이 없어 못 함」이 되고, 하네스는 그걸 「기능이 고장났다」고 적는다(실측: 함정 24/25 로 실패).
		/// ★ 값을 채우는 것은 *배치 규칙을 우회하지 않는다* — 자리·보급·암반 검사는 그대로 통과해야 한다.
		/// </summary>
		private static void EnsureBudget(int needed)
		{
			if (match != null && match.Resource < needed)
				match.GrantForVerification(needed - match.Resource, 0);
		}

		/// <summary> 함정 — 깔리는가(길목에 소모품을 놓는 수단이 실제로 존재하는가). </summary>
		private static void VerifyTrap()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			EnsureBudget(match.Stage.TrapCost * 2);
			int placed = 0;
			int before = match.Resource;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 2))
			{
				if (match.TryPlaceTrap(stageRoot.TransformPoint(local.ToUnity()).ToSim()))
					placed++;
			}

			string verdict = TAG + " TRAP placed=" + placed + " resource " + before + " → " + match.Resource;
			if (placed > 0 && match.Resource < before)
				Debug.Log(verdict + " ✔");
			else
				Debug.LogError(verdict + " → 함정이 안 깔리거나 값을 안 치른다.");
		}

		/// <summary>
		/// 벽 — 세워지는가, 그리고 *길을 완전히 막는 자리는 거절되는가*.
		/// 후자가 핵심 불변식이다(막히면 마수가 굳어 웨이브가 영영 안 끝난다).
		/// 코어를 빙 둘러 벽으로 감싸보고 마지막 한 칸이 거절되는지로 확인한다.
		/// </summary>
		private static void VerifyWall()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// ★ 예산을 먼저 채운다 — 돈이 없어서 거절된 것을 「길을 끊어서 거절됐다」로 읽으면
			//   이 검사는 *거짓으로 통과*한다(잔고가 불변식을 대신 증명해 버린다).
			EnsureBudget(match.Stage.WallCost * 12);

			// ① 평범한 자리에 한 장 — 서야 한다.
			int placed = 0;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 3))
			{
				if (match.TryPlaceWall(stageRoot.TransformPoint(local.ToUnity()).ToSim()))
					placed++;
			}

			// ② 코어를 완전히 감싸본다 — 마지막에 반드시 막혀야(거절돼야) 한다.
			int accepted = 0;
			int rejected = 0;
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					if (offsetX == 0 && offsetY == 0)
						continue;
					Vector3 local = new Vector3(offsetX + 0.5f, 0f, offsetY + 0.5f);
					if (match.TryPlaceWall(stageRoot.TransformPoint(local.ToUnity()).ToSim()))
						accepted++;
					else
						rejected++;
				}
			}

			string verdict = TAG + " WALL placed=" + placed + " ringAccepted=" + accepted + " ringRejected=" + rejected;
			if (placed > 0 && rejected > 0)
				Debug.Log(verdict + " → 벽은 서고, 길을 끊는 자리는 거절된다 ✔");
			else
				Debug.LogError(verdict + " → 벽이 안 서거나(placed=0) 코어를 완전히 봉인할 수 있다(rejected=0).");
		}

		/// <summary> 판매 — 세운 것을 팔면 자원이 돌아오고 자리가 비는가(정착 후에 확인). </summary>
		private static void VerifySell()
		{
			Transform stageRoot = FindStageRoot();
			if (sellProbeReady == false || match == null || stageRoot == null)
				return;

			Vector3 world = stageRoot.TransformPoint(sellProbeLocal.ToUnity()).ToSim();
			int before = match.Resource;
			bool sold = match.TrySell(world, match.Stage.SellRefundRatio);
			bool freed = match.IsCellOccupied(world) == false;

			string verdict = TAG + " SELL ok=" + sold + " resource " + before + " → " + match.Resource
				+ " soldValue=" + match.LastSoldValue + " ratio=" + match.Stage.SellRefundRatio.ToString("F2")
				+ " refund=" + match.LastSellRefund + " cellFreed=" + freed;
			if (sold && match.Resource > before && freed)
				Debug.Log(verdict + " → 되돌릴 수 있다 ✔");
			else
				Debug.LogError(verdict + " → 판매가 안 되거나 자리가 안 비었다.");
		}
	}
}
