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
	// TowerDefenseMatch 의 Build 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		private readonly List<Vector2Int> pathGoals = new();

		// 세워둔 것들의 사거리 원 — 기본은 전부 꺼져 있고, 묻는 순간(마우스 얹기)에만 하나가 켜진다.
		private readonly List<TowerDefenseRing> rangeRings = new();
		private bool showAllRanges;
		private int nextDollOrdinal;

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

		// 부서진 자리를 알리려면 *부서지기 전* 자리를 알아야 한다 — 사라진 뒤엔 물어볼 데가 없다.
		private readonly Dictionary<Transform, Vector3> lastBuildingPositions = new();

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

		// ── 판 기록 ───────────────────────────────────────────────────────────────
		// ★ 왜 필요한가 (개선 목록 24번): 지금은 지고 나면 「몇 분 버팀」 한 줄뿐이라 *왜 졌는지*를
		//   되짚을 수단이 없다. 무엇을 몇 개 지었고, 몇 개를 잃었고, 마수가 가장 많을 때 몇이었는지가
		//   남아야 다음 판이 달라진다 — 안 남으면 매 판이 같은 실수의 반복이 된다.
		// 방금 판 인형들 — 다음 정리에서 「잃음」으로 세지 않기 위한 표시.
		private readonly HashSet<TowerDefenseDollLabel> soldDolls = new();

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

		/// <summary> 이번 판의 암반 칸 수 — 0 이면 지형 없는 빈 판. </summary>
		public int ObstacleCount => mapLayout != null ? mapLayout.ObstacleCells.Count : 0;

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
