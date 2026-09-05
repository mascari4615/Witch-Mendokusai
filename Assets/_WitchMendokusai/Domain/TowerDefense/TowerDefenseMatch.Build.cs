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
	// TowerDefenseMatch 의 터 고르기와 짓기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		private readonly List<Vector2Int> pathGoals = new();

		// 내가 세운 벽. 암반(생성된 지형)과 합쳐 「통행 불가」 하나로 본다 —
		// 길찾기·표시·배치가 각자 다른 기준을 쓰면 화면과 규칙이 갈라진다.
		private readonly HashSet<Vector2Int> wallCells = new();

		// 격자 A* — 목표가 코어든 벽이든 *실제 경로*를 찾는다. 흐름장(코어 전용)을 대신한다.
		private TowerDefenseGridPath gridPath;
		private float activeGroundWidth;
		private float activeGroundLength;

		// 셀 점유(TASK-WM-194 증분3) — 타워/채집건물 배치는 한 셀에 하나만(겹배치 차단). 키 = FloorToInt 셀(y=0 고정,
		// 층 무관 단일 격자). claimedNodes(자원 노드 자체 점유)와 직교 — 이건 "그 좌표에 뭔가 이미 서 있나"만 본다.
		private readonly HashSet<Vector3Int> occupiedCells = new();

		/// <summary> 지금 판의 크기(월드 단위) — 미니맵이 좌표를 비율로 바꿀 때 쓴다. 판이 자라면 같이 커진다. </summary>
		public float GroundWidth => activeGroundWidth;
		public float GroundLength => activeGroundLength;

		/// <summary> 통행 불가 판정 — 생성된 암반 + 내가 세운 벽. 길찾기·표시가 같은 함수를 본다. </summary>
		private bool IsPathBlocked(Vector2Int cell)
		{
			return mapLayout.IsBlocked(cell) || wallCells.Contains(cell);
		}

		/// <summary>
		/// 그 자리에 *무엇이든* 세울 수 있나 — 판 안인가 · 암반이 아닌가 · 내 땅인가.
		///
		/// ★ 왜 한 곳으로 모았나: 여섯 배치 경로가 이 검사를 각자 베껴 쓰고 있었고, 그러다 보니
		///   경로마다 빠진 것이 달랐다(함정은 판 끝 검사가 없어 판 밖에 깔렸고, 벽은 암반 위에 섰다.
		///   둘 다 「보급이 닿는 곳에만」 규칙 밖이었다 — 그건 사용자가 명시적으로 요청한 규칙이다).
		///   검사가 여러 벌이면 새 배치를 추가할 때마다 한 벌이 또 빠진다.
		/// 점유·값은 여기 없다 — 경로마다 다르게 취급한다(포탑은 같은 칸이면 승급, 채집은 노드로 스냅).
		/// </summary>
		private bool ValidateSite(Vector3 worldPosition)
		{
			if (CanBuildAt(worldPosition))
				return true;

			// 여기 왔으면 셋 중 하나가 막은 것 — 어느 것인지만 골라 말한다.
			if (IsInsideWindow(worldPosition) == false)
				return Reject("판 끝이다 — 여기부터는 아직 열리지 않았다", worldPosition);
			if (IsObstacleAt(worldPosition))
				return Reject("암반 위엔 못 짓는다", worldPosition);
			return Reject("보급이 닿는 곳에만 지을 수 있다", worldPosition);
		}

		/// <summary>
		/// 그 자리에 지을 수 있나 — *조용한* 판정. 미리보기가 매 프레임 묻는다(거절 사유를 쏟으면 안 된다).
		///
		/// ★ 규칙 자체는 이 한 줄이 전부다. 예전엔 미리보기가 같은 판정을 자기 손으로 다시 조립했고,
		///   그러다 **판 끝 검사를 빠뜨렸다** — 가장자리에서 초록불이 켜지는데 실제로는 거절됐다.
		///   화면이 「여기 된다」고 해놓고 안 되면 그 화면을 믿을 수 없게 된다.
		/// 칸이 찼는지는 여기 없다 — 경로마다 다르게 취급한다(포탑은 같은 칸이면 승급).
		/// </summary>
		public bool CanBuildAt(Vector3 worldPosition)
		{
			return IsMatchOver == false
				&& IsInsideWindow(worldPosition)
				&& IsObstacleAt(worldPosition) == false
				&& IsInBuildableRange(worldPosition);
		}

		/// <summary>
		/// 함정 깔기 — 밟으면 터진다. 길목과 직결되므로 벽(길 그리기)의 짝.
		/// 통행을 막지 않으므로 길 검사가 필요 없다(그래서 벽보다 훨씬 가볍다).
		/// </summary>
		public bool TryPlaceTrap(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;

			int trapCost = CostOf(TowerDefensePlaceableKind.Trap);
			if (core.TrySpend(trapCost) == false)
				return Reject($"자원 부족 {core.Resource}/{trapCost}", worldPosition);

			occupiedCells.Add(cellKey);
			BuildTrapObject(worldPosition, cellKey);
			return true;
		}

		private void BuildTrapObject(Vector3 worldPosition, Vector3Int cellKey)
		{
			float cellSize = stage.GroundCellSize;
			GameObject trapObject = TowerDefenseVisuals.Primitive(PrimitiveType.Quad);
			trapObject.name = "Trap";
			Destroy(trapObject.GetComponent<Collider>()); // 밟는 판정은 거리로 한다 — 물리를 끼우면 마수가 걸린다.
			trapObject.transform.SetParent(stageRoot, false);
			trapObject.transform.position = (worldPosition + new Vector3(0f, 0.05f, 0f)).ToUnity();
			trapObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			trapObject.transform.localScale = (Vector3.one * cellSize * 0.85f).ToUnity();

			Renderer trapRenderer = trapObject.GetComponent<Renderer>();
			if (trapRenderer != null)
			{
				Material trapMaterial = new Material(trapRenderer.sharedMaterial);
				TowerDefenseVisuals.MakeTransparent(trapMaterial);
				Color trapColor = stage.TrapTint;
				trapColor.a = 0.75f;
				trapMaterial.color = trapColor;
				if (trapMaterial.HasProperty("_BaseColor"))
					trapMaterial.SetColor("_BaseColor", trapColor);
				trapRenderer.sharedMaterial = trapMaterial;
			}

			TowerDefenseTrap trap = trapObject.AddComponent<TowerDefenseTrap>();
			// ★ 함정도 전기를 먹는다 — 벽·함정만 체계 밖에 있으면 「전기가 부족하면 방어가 선다」는 규칙이
			//   반쪽이 된다. 전기가 끊긴 함정은 밟혀도 안 터진다.
			powerGrid.AddConsumer(trapObject.transform);

			trap.Configure(waveEnemies, Mathf.RoundToInt(stage.TrapDamage * boons.TrapPowerMultiplier), stage.TrapCharges, stage.TrapRadius,
				spent =>
				{
					// 다 쓴 함정은 자리를 비워준다 — 안 비우면 그 칸이 영영 죽는다.
					occupiedCells.Remove(cellKey);
					TrapsSpent++;
					if (spent != null)
						Destroy(spent.gameObject);
				});
		}

		/// <summary> 다 쓰고 사라진 함정 수 — 검증·통계용. </summary>
		public int TrapsSpent { get; private set; }

		/// <summary>
		/// 벽 세우기 — 마수의 길을 *내가 그린다*. 장르적으로 여기가 가장 큰 전환점이다:
		/// 「어디에 지을까」가 「길을 어떻게 낼까」로 승격된다.
		///
		/// ★ 단 하나의 불변식: **길을 완전히 막을 수는 없다.** 모든 출현 지점에서 코어까지 가는 길이
		///   남아야 한다. 안 그러면 마수가 벽 앞에 굳고 웨이브가 영원히 안 끝난다(이미 겪은 사고).
		///   그래서 *먼저 세워보고 길이 남는지 확인한 뒤* 확정한다 — 안 되면 원상복구하고 거절.
		/// </summary>
		public bool TryPlaceWall(Vector3 worldPosition)
		{
			if (core == null || mapLayout == null || stageRoot == null)
				return false;

			Vector3Int cellKey = ToCellKey(worldPosition);
			if (occupiedCells.Contains(cellKey))
				return Reject("여긴 이미 찼다", worldPosition);
			if (ValidateSite(worldPosition) == false)
				return false;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim());
			if (mapLayout.IsInside(cell) == false || IsPathBlocked(cell))
				return false;

			wallCells.Add(cell);
			if (RebuildPathing() == false)
			{
				wallCells.Remove(cell); // 길이 끊긴다 — 없던 일로.
				RebuildPathing();
				Debug.Log($"{nameof(TowerDefenseMatch)}: 벽 거절 — 여길 막으면 마수가 코어까지 갈 길이 없다.");
				return false;
			}

			if (core.TrySpend(CostOf(TowerDefensePlaceableKind.Wall)) == false)
			{
				wallCells.Remove(cell);
				RebuildPathing();
				return false;
			}

			occupiedCells.Add(cellKey);
			BuildWallObject(cell);
			return true;
		}

		private bool RebuildPathing()
		{
			pathGoals.Clear();
			pathGoals.Add(mapLayout.CoreCell);
			AddApproachRing(mapLayout.CoreCell);
			foreach (Transform outpost in outposts)
			{
				if (outpost != null)
					pathGoals.Add(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(outpost.position).ToSim()));
			}

			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, pathGoals, IsPathBlocked);
			gridPath = new TowerDefenseGridPath(mapLayout.Width, mapLayout.Length, IsPathBlocked);
			flowNavigator = new TowerDefensePathNavigator(
				mapLayout, gridPath, stageRoot, stage.GroundCellSize * 2f, stage.EnemyCornerSmoothing);

			foreach (Vector3 spawnLocal in activeSpawnPoints)
			{
				if (flowField.IsReachable(mapLayout.WorldToCell(spawnLocal)) == false)
					return false;
			}

			// ★ 파도는 이제 **테두리 어디서든** 온다(테두리 침공). 그런데 이 검사는 옛 개념인 *둥지 자리*만
			//   보고 있었다 — 그래서 다음 파도가 올 토막을 벽으로 막아도 통과했고, 그러면 그 파도는
			//   길이 없는 자리에서 태어나 판이 교착된다. 「어디서든 온다」면 **테두리 전체**를 봐야 한다.
			if (stage.BorderInvasion && IsBorderReachable() == false)
				return false;

			// 이미 걷고 있는 마수도 새 길을 따라야 한다 — 안 그러면 벽 안쪽에 갇힌다.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.Navigator = flowNavigator;
			}

			terrainView.BuildPathLanes();
			return true;
		}

		private void BuildWallObject(Vector2Int cell)
		{
			float cellSize = mapLayout.CellSize;
			GameObject wall = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
			wall.name = "Wall";
			wall.transform.SetParent(stageRoot, false);
			wall.transform.localPosition = (mapLayout.CellToWorld(cell) + new Vector3(0f, cellSize * 0.35f, 0f)).ToUnity();
			wall.transform.localScale = new Vector3(cellSize * 0.94f, cellSize * 0.7f, cellSize * 0.94f).ToUnity();

			Renderer wallRenderer = wall.GetComponent<Renderer>();
			if (wallRenderer == null)
				return;

			Color wallColor = stage.WallTint;
			Material wallMaterial = new Material(wallRenderer.sharedMaterial);
			wallMaterial.color = wallColor;
			if (wallMaterial.HasProperty("_BaseColor"))
				wallMaterial.SetColor("_BaseColor", wallColor);
			wallRenderer.sharedMaterial = wallMaterial;

			// 벽도 보급 중계다 — 길을 그리는 것과 보급선을 잇는 것이 같은 행위가 되어,
			// 「어디에 벽을 세울까」가 방어선과 살림살이 양쪽을 동시에 결정한다.
			supplyChain.Add(wall.transform);
			RefreshSupply();
		}


		/// <summary> URP Lit 재질을 반투명으로 — 불투명 그대로면 길 표시가 바닥을 덮어버린다. </summary>


		/// <summary> 지면(바닥) 런타임 생성 — RectangleArenaMap.Build 와 동형(Plane 스케일, SO 수치 그대로). </summary>
		private void BuildGround()
		{
			// 그리는 일은 통째로 다른 층이 한다 — 여기 남는 것은 「무엇을 그릴지」 넘겨주는 일뿐이다.
			terrainView.Configure(stageRoot, stage, mapLayout, flowField, activeSpawnPoints, activeNodePositions);
			terrainView.Build(activeGroundWidth, activeGroundLength);

			if (vision != null)
			{
				// ★ 안개는 *땅을 어둡게* 하는 것이지 인형을 덮는 판때기가 아니다(사용자 실증:
				//   "안개랑 길도 마찬가지. 뭐 유닛들 가리고 난리났어. 롤처럼 오브젝트 아예 안보이게
				//   하던지 해야지 판떼기로 가리려고 하지 않았으면"). 높이를 인형 머리 위(0.9)에서
				//   땅 바로 위로 내린다 — 못 본 자리의 *개체*는 렌더러를 꺼서 감춘다(ApplyEnemyVisibility).
				fogView = TowerDefenseFogView.Create(
					stageRoot, mapLayout.Width, mapLayout.Length, activeGroundWidth, activeGroundLength, stage.FogHeight);
				RefreshVision();
			}
		}

		/// <summary> 대여 계약 부착 + 원본 스냅샷 — 멱등(이미 붙어 있으면 재사용, 스냅샷은 최초 1회만). </summary>
		private static void AcquireLease(UnitObject unitObject)
		{
			TowerDefenseUnitLease lease = unitObject.GetComponent<TowerDefenseUnitLease>();
			if (lease == null)
				lease = unitObject.gameObject.AddComponent<TowerDefenseUnitLease>();
			lease.Acquire(unitObject);
		}

		/// <summary>
		/// 안내가 「길 없음」으로 끝난 횟수 — 앞을 막은 것을 부수러 붙는 중이면 정상이고,
		/// 아무도 안 부수고 서 있으면 판이 안 끝난다. 이 값과 「굳은 마수 수」를 같이 봐야 가려진다.
		/// </summary>
		public int NavigatorNoPathCount => flowNavigator is TowerDefensePathNavigator pathNavigator
			? pathNavigator.NoPathCount
			: 0;

		/// <summary> 길찾기가 상한에 걸려 포기한 횟수 — 0 이 아니면 갈 길이 있는데도 못 가는 마수가 있다. </summary>
		public int PathCapHits => gridPath != null ? gridPath.CapHits : 0;

		/// <summary> 한 번의 길찾기에서 가장 많이 펼친 칸 수 — 상한(기본 4000)에 얼마나 가까운지. </summary>
		public int PathPeakCells => gridPath != null ? gridPath.PeakExpandedCells : 0;

		/// <summary> 지금 판에 깔려 있는 함정 수 — 이어하기가 함정을 잃는지 하네스가 직접 센다. </summary>
		public int TrapCount => stageRoot != null ? stageRoot.GetComponentsInChildren<TowerDefenseTrap>(true).Length : 0;

		/// <summary> 지금 판의 벽 칸 수 — 같은 이유. </summary>
		public int WallCellCount => wallCells.Count;

		/// <summary>
		/// 거기에 지을 수 있는가 — **보급이 닿는 곳에만** 지을 수 있다.
		///
		/// ★ 왜 필요한가 (사용자 지시: "설치할 수 있는 범위가 제한이 되어야 할 것 같은데. 지금 그냥 맨 땅에
		///   설치할 수 있으니까 문제"): 아무 데나 지을 수 있으면 개척이라는 말이 성립하지 않는다. 마수가
		///   나오는 자리 옆에 바로 포탑을 박으면 길목도, 넓히는 결정도, 보급선도 전부 의미를 잃는다.
		/// ★ 왜 *보급* 기준인가: 이미 있는 규칙을 그대로 쓴다. 코어·전초기지·이어진 내 건물에서 뻗어 나가는
		///   것이 곧 「내 땅」이고, 화면에 그려둔 보급 사거리 원이 그 경계를 이미 보여주고 있다.
		///   새 숫자를 만들면 화면의 원과 실제 규칙이 갈라진다.
		/// </summary>
		public bool IsInBuildableRange(Vector3 worldPosition)
		{
			if (stage == null || coreCombatant == null)
				return true;

			return supplyChain.IsWithinReach(worldPosition, coreCombatant.Position, outposts, EffectiveSupplyReach);
		}

		/// <summary> 이번 판의 암반 칸 수 — 0 이면 지형 없는 빈 판. </summary>
		public int ObstacleCount => mapLayout != null ? mapLayout.ObstacleCells.Count : 0;

		/// <summary>
		/// worldPosition 이 속한 셀이 이미 배치물로 점유됐는지 — 배치 UI 프리뷰가 유효/무효 색을
		/// 이 메서드로 판정(TryPlaceTower/TryPlaceHarvester 내부 점유 판정과 동일 규칙 재사용).
		/// </summary>
		public bool IsCellOccupied(Vector3 worldPosition)
		{
			if (occupiedCells.Contains(ToCellKey(worldPosition)))
				return true;

			// 암반 위에는 못 짓는다 — 화면에 바위가 보이는데 그 위에 세워지면 규칙과 그림이 어긋난다.
			return IsObstacleAt(worldPosition);
		}

		/// <summary> 그 자리가 암반인지(무대 로컬 환산 후 판정). 고정 판이면 항상 false. </summary>
		public bool IsObstacleAt(Vector3 worldPosition)
		{
			if (mapLayout == null || stageRoot == null)
				return false;

			return mapLayout.IsBlocked(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim());
		}

		// 셀 키 = FloorToInt(worldPosition), y 는 0 고정(층 무관 단일 격자 — 위로 쌓기 원천 차단).
		private static Vector3Int ToCellKey(Vector3 worldPosition)
		{
			Vector3Int cell = Vector3Int.FloorToInt(worldPosition);
			cell.y = 0;
			return cell;
		}
	}
}
