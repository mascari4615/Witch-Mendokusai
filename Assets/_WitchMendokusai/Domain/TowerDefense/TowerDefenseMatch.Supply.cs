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
	// TowerDefenseMatch 의 보급과 전력 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
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

		// 보급 원점(코어·전초기지)의 원 — 보급 거리 연구가 오르면 이것도 같이 자라야 한다.
		private readonly List<TowerDefenseRing> supplyRings = new();

		private readonly List<Vector3> activeNodePositions = new();
		private readonly List<float> activeNodeIncomeMultipliers = new();
		// 노드 등급 — 바깥 노드는 정수를 낸다(안쪽은 자원). 「멀리 나가야 강해진다」의 근거.
		private readonly List<bool> activeNodeIsOuter = new();

		/// <summary> 전초기지 위치들 — 미니맵이 「내가 넓힌 곳」을 그린다. </summary>
		public IReadOnlyList<Transform> Outposts => outposts;
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

		// 카드로 늘린 전기 용량 — 코어가 대주는 양에 더해진다.
		private int bonusPowerCapacity;

		/// <summary> 이 건물이 전기를 받고 있나 — 채집 수입이 이 값을 본다. </summary>
		private bool IsPowered(Transform building)
		{
			if (stage == null || stage.CorePowerCapacity <= 0)
				return true;

			return powerGrid.IsPowered(building);
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
	}
}
