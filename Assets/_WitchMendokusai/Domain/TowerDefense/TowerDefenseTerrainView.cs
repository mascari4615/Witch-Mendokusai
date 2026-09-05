using System.Collections.Generic;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 판을 *그리는* 층 — 바닥·암반·길 표시·자원/출현 표식 (TASK-WM-194).
	///
	/// ★ 왜 떼어냈나: 전기·보급에 이은 세 번째 증분. 이 무리는 경제·전투와 아무것도 주고받지 않는다 —
	///   판 데이터를 읽어 씬에 물건을 세울 뿐이다. 그런 것이 4000줄 본체 안에 섞여 있으면
	///   「무엇이 규칙이고 무엇이 그림인가」가 흐려진다(오늘 회색 화면 사고가 정확히 그 경계에서 났다).
	/// ★ 안개·시야는 여기 없다 — 그건 *규칙이 있는* 층이라(안 보이면 못 쏜다) 본체가 계속 쥔다.
	/// </summary>
	public sealed class TowerDefenseTerrainView
	{
		private Transform stageRoot;
		private TowerDefenseStageSO stage;
		private TowerDefenseMapLayout mapLayout;
		private TowerDefenseFlowField flowField;
		private IReadOnlyList<Vector3> activeSpawnPoints;
		private IReadOnlyList<Vector3> activeNodePositions;
		private Transform laneRoot;

		/// <summary> 이번 판의 재료를 받아둔다 — 판이 자라면 다시 부른다. </summary>
		public void Configure(Transform root, TowerDefenseStageSO stageConfig, TowerDefenseMapLayout layout,
			TowerDefenseFlowField field, IReadOnlyList<Vector3> spawnPoints, IReadOnlyList<Vector3> nodePositions)
		{
			stageRoot = root;
			stage = stageConfig;
			mapLayout = layout;
			flowField = field;
			activeSpawnPoints = spawnPoints;
			activeNodePositions = nodePositions;
		}

		/// <summary> 바닥부터 표식까지 한 번에 세운다. </summary>
		public void Build(float groundWidth, float groundLength)
		{
			GameObject ground = TowerDefenseVisuals.Primitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(stageRoot, false);
			ground.transform.localPosition = Vector3.zero.ToUnity();
			ground.transform.localScale = GroundPlane.ScaleFor(groundWidth, groundLength);

			ApplyGroundCheckerboard(ground, groundWidth, groundLength);
			BuildObstacles();
			BuildPathLanes();
			BuildResourceNodeMarkers();
			BuildEnemySpawnMarkers();
		}

		/// <summary>
		/// 마수가 실제로 걸어올 길을 바닥에 깐다 — 「여기가 길목」이 한눈에 보여야 배치가 판단이 된다.
		///
		/// ★ 길 안내판(흐름장)을 그대로 따라가며 칠하므로 *표시와 실제 이동이 같은 출처*다.
		///   보기용으로 따로 그리면 언젠가 반드시 어긋나고, 그때 화면은 플레이어를 속인다.
		/// 여러 출현 지점의 길이 겹치는 칸일수록 진하게 — 겹치는 곳이 곧 최고의 포탑 자리다.
		/// </summary>
		public void BuildPathLanes()
		{
			if (mapLayout == null || flowField == null)
				return;

			// 벽을 세울 때마다 다시 그리므로 지난 표시를 먼저 치운다(안 치우면 옛 길이 겹쳐 남는다).
			if (laneRoot != null)
				Object.Destroy(laneRoot.gameObject);
			laneRoot = new GameObject("PathLanes").transform;
			laneRoot.SetParent(stageRoot, false);

			Dictionary<Vector2Int, int> laneWeight = new();
			foreach (Vector3 spawnLocal in activeSpawnPoints)
			{
				Vector2Int cell = mapLayout.WorldToCell(spawnLocal);
				int guard = mapLayout.Width * mapLayout.Length;

				while (guard-- > 0 && cell != flowField.GoalCell)
				{
					laneWeight.TryGetValue(cell, out int weight);
					laneWeight[cell] = weight + 1;

					if (flowField.TryGetNextCell(cell, out Vector2Int next) == false)
						break;
					cell = next;
				}
			}

			float cellSize = mapLayout.CellSize;
			foreach ((Vector2Int cell, int weight) in laneWeight)
			{
				GameObject lane = TowerDefenseVisuals.Primitive(PrimitiveType.Quad);
				lane.name = "PathLane";
				Object.Destroy(lane.GetComponent<Collider>()); // 표시용 — 배치 레이캐스트를 가로채면 안 된다.
				lane.transform.SetParent(laneRoot, false);
				lane.transform.localPosition = (mapLayout.CellToWorld(cell) + new Vector3(0f, 0.03f, 0f)).ToUnity();
				lane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				lane.transform.localScale = (Vector3.one * cellSize * 0.92f).ToUnity();

				Renderer laneRenderer = lane.GetComponent<Renderer>();
				if (laneRenderer == null)
					continue;

				// 겹칠수록 진하게(1갈래 = 옅게, 여러 갈래 = 뚜렷하게).
				// 어두운 바닥 위에서 확실히 튀는 밝기까지 올린다 — 길이 안 보이면 이 기능은 없는 것과 같다.
				// ★ 길은 *불투명*으로 그린다 — 반투명이면 스프라이트(인형·마수)와 같은 줄에 서서
				//   그리는 순서를 다투고, 그러다 인형을 덮는다(사용자 실증: "길도 유닛이나 건물을 가림").
				//   안개와 같은 결론이다: 바닥에 속한 것은 바닥의 줄에 있어야 한다.
				//   진하기는 알파가 아니라 *색*으로 낸다 — 어두운 바닥과 섞어 미리 계산한다.
				float intensity = Mathf.Clamp01(0.6f + (weight - 1) * 0.2f);
				Color groundTone = new Color(0.28f, 0.32f, 0.38f, 1f);
				Color laneColor = Color.Lerp(groundTone, new Color(1f, 0.74f, 0.28f, 1f), intensity);
				laneColor.a = 1f;
				Material laneMaterial = new Material(laneRenderer.sharedMaterial);
				laneMaterial.color = laneColor;
				if (laneMaterial.HasProperty("_BaseColor"))
					laneMaterial.SetColor("_BaseColor", laneColor);
				laneRenderer.sharedMaterial = laneMaterial;
				laneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				// ★ 그리는 *순서*만 바꾸면 인형이 계속 잘린다 — 불투명한 판이 남긴 **깊이**에 스프라이트가
				//   걸리기 때문이다(사용자 실증: "길이 공중에 떠 있어서 유닛이 중간에 짤린다").
				//   왜 순서로는 안 되는지 = MakeFloorDecal 의 ★ 주석. 이 호출이 한 번 사라진 적이 있다 — 지우지 말 것.
				TowerDefenseVisuals.MakeFloorDecal(laneRenderer.sharedMaterial);
			}
		}

		/// <summary>
		/// 암반 세우기 — 눈에 보이고(길목이 읽혀야 배치 판단이 생김) 실제로 막는다(콜라이더).
		/// 칸 하나당 상자 하나 = 셀 격자와 정확히 일치 → 「저 칸은 못 지나간다」가 화면과 규칙에서 같다.
		/// </summary>
		private void BuildObstacles()
		{
			if (mapLayout == null)
				return;

			float cell = mapLayout.CellSize;
			// 어두운 바닥 위 어두운 바위 = 안 보인다(라이브 스크린샷 실증 — 암반과 지면이 구분 안 됨).
			// 벽이라는 걸 알려면 바닥보다 확실히 밝고 따뜻해야 하고, 높이도 눈에 띄게 서 있어야 한다.
			Color rockColor = new Color(0.62f, 0.55f, 0.47f, 1f);
			Material rockMaterial = null;

			foreach (Vector2Int obstacleCell in mapLayout.ObstacleCells)
			{
				GameObject rock = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
				rock.name = "Rock";
				rock.transform.SetParent(stageRoot, false);
				// 높은 벽은 부감 시점에서 뒤쪽 바닥을 가려 길 표시를 통째로 먹는다(스크린샷 실증).
				// 낮은 능선이면 「막힌 칸」은 그대로 읽히면서 길이 보인다.
				rock.transform.localPosition = (mapLayout.CellToWorld(obstacleCell) + new Vector3(0f, cell * 0.3f, 0f)).ToUnity();
				rock.transform.localScale = new Vector3(cell, cell * 0.6f, cell).ToUnity();

				// ★ 충돌 상자를 칸보다 작게 — 길찾기는 1칸 통로를 정상 경로로 주는데, 몸통이 칸을 꽉 채우면
				//   그 통로에서 물리적으로 낀다(라이브: 마수 1기가 40초 가까이 도착 못 함). 보이는 크기는 그대로.
				// ★ 이 값이 코드에 박혀 있어서(0.82) 여유가 얼마나 남는지를 아무도 조절할 수 없었다.
				//   마수 몸 반경이 0.50 이므로 남는 여유 = 1.00 - 0.50 - (값/2). 0.82 면 0.09 뿐이라,
				//   무리에 밀려 반 칸만 치우쳐도 바위를 스치고 그 자리에서 판이 끝날 때까지 민다(실측).
				BoxCollider rockCollider = rock.GetComponent<BoxCollider>();
				if (rockCollider != null)
				{
					float side = stage != null ? stage.ObstacleColliderScale : 0.82f;
					rockCollider.size = new Vector3(side, 1f, side).ToUnity();
				}

				Renderer rockRenderer = rock.GetComponent<Renderer>();
				if (rockRenderer == null)
					continue;

				// 재질 1장을 전부가 공유 — 칸마다 새 재질을 만들면 수백 장이 된다.
				if (rockMaterial == null)
				{
					rockMaterial = new Material(rockRenderer.sharedMaterial);
					rockMaterial.color = rockColor;
					if (rockMaterial.HasProperty("_BaseColor"))
						rockMaterial.SetColor("_BaseColor", rockColor);
				}
				rockRenderer.sharedMaterial = rockMaterial;
			}
		}

		/// <summary>
		/// 바닥 체크무늬 — 배치는 1칸 격자에 스냅되는데 바닥이 민무늬면 "어디가 한 칸인지" 알 수 없다
		/// (사용자 실증: "땅은 격자나 체크무늬가 없어서 어디가 구분인지도 모르겠다").
		/// 텍스처를 코드로 생성 = 아트 에셋 의존 0. 타일링을 스테이지 칸 크기에 맞춰 *보이는 칸 = 배치 칸*
		/// 이 되게 한다(둘이 어긋나면 격자가 오히려 거짓말을 한다).
		/// </summary>
		private void ApplyGroundCheckerboard(GameObject ground, float groundWidth, float groundLength)
		{
			Renderer groundRenderer = ground.GetComponent<Renderer>();
			if (groundRenderer == null)
				return;

			// 한 칸 = 텍스처 1장. 칸 경계에 밝은 선을 그어 격자를 *선으로* 보이게 한다
			// (2x2 체크무늬는 화면에서 거의 안 읽혔다 — 사용자 실증 "바닥 격자 좀 만들어줘").
			// 체크 음영도 함께 넣어 짝수/홀수 칸이 구분되게.
			// ★ 진짜 체스판으로 (사용자 지시: "체스판처럼 게임 프로토타입에서 많이 보이는 텍스쳐").
			//   격자 *선*만 그으면 칸이 다 같은 색이라 「몇 칸 떨어졌나」가 안 읽힌다. 밝은 칸/어두운 칸이
			//   번갈아 나오면 거리가 눈으로 세어진다 — 프로토타입 바닥이 늘 체스판인 이유가 그거다.
			//   텍스처 한 장 = 2×2 칸(체크 한 주기).
			const int CELL_PIXELS = 32;
			const int LINE_PIXELS = 2;
			const int TEXTURE_PIXELS = CELL_PIXELS * 2;
			Texture2D checker = new Texture2D(TEXTURE_PIXELS, TEXTURE_PIXELS, TextureFormat.RGBA32, mipChain: true)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Repeat,
			};
			Color lightCell = new Color(0.34f, 0.38f, 0.44f, 1f);
			Color darkCell = new Color(0.22f, 0.25f, 0.30f, 1f);
			Color gridLine = new Color(0.55f, 0.62f, 0.72f, 1f);
			for (int y = 0; y < TEXTURE_PIXELS; y++)
			{
				for (int x = 0; x < TEXTURE_PIXELS; x++)
				{
					bool oddCell = (x / CELL_PIXELS + y / CELL_PIXELS) % 2 == 1;
					int inCellX = x % CELL_PIXELS;
					int inCellY = y % CELL_PIXELS;
					bool onEdge = inCellX < LINE_PIXELS || inCellY < LINE_PIXELS;
					checker.SetPixel(x, y, onEdge ? gridLine : (oddCell ? darkCell : lightCell));
				}
			}
			checker.Apply();

			// 텍스처 한 장 = 2칸이므로 타일 수 = 전체 길이 / (칸크기 × 2).
			float cell = stage.GroundCellSize > 0f ? stage.GroundCellSize : 1f;
			Vector2 tiling = new Vector2(groundWidth / (cell * 2f), groundLength / (cell * 2f));

			Material groundMaterial = groundRenderer.material;
			groundMaterial.mainTexture = checker;
			groundMaterial.mainTextureScale = tiling;
			// URP Lit 는 _BaseMap/_BaseColor 가 정본 — mainTexture 만 세팅하면 셰이더에 따라 안 먹을 수 있다.
			if (groundMaterial.HasProperty("_BaseMap"))
			{
				groundMaterial.SetTexture("_BaseMap", checker);
				groundMaterial.SetTextureScale("_BaseMap", tiling);
			}
			if (groundMaterial.HasProperty("_BaseColor"))
				groundMaterial.SetColor("_BaseColor", Color.white);
		}

		/// <summary>
		/// 자원 노드 표식 — 채집 인형은 노드 반경 안에만 설 수 있는데 노드가 안 보이면 플레이어가
		/// 어디를 클릭할지 알 수 없다(플레이 불가). 시각 표식은 순수 연출이라 콜라이더 제거 —
		/// 배치 레이캐스트를 가로채면 스냅 좌표가 표식 표면 기준으로 튄다.
		/// stageRoot 자식이라 Dispose 의 자식 파괴 경로가 그대로 정리한다.
		/// </summary>
		private void BuildResourceNodeMarkers()
		{
			foreach (Vector3 localPosition in activeNodePositions)
			{
				GameObject marker = TowerDefenseVisuals.Primitive(PrimitiveType.Cylinder);
				marker.name = "ResourceNode";
				Collider markerCollider = marker.GetComponent<Collider>();
				if (markerCollider != null)
					Object.Destroy(markerCollider);

				marker.transform.SetParent(stageRoot, false);
				marker.transform.localPosition = localPosition.ToUnity();
				// 납작한 원반 — 지면에 깔리되 유닛 시야를 안 가림.
				marker.transform.localScale = new Vector3(stage.NodeCaptureRadius * 2f, 0.05f, stage.NodeCaptureRadius * 2f).ToUnity();

				// URP Lit 는 _BaseColor 가 정본 — material.color 만 세팅하면 셰이더에 따라 안 먹는다.
				Renderer markerRenderer = marker.GetComponent<Renderer>();
				if (markerRenderer != null)
				{
					Material markerMaterial = markerRenderer.material;
					Color nodeColor = new Color(1f, 0.82f, 0.25f, 1f); // 금빛 = "여기서 캔다". 바닥(회색)·아군(파랑)·적(빨강) 과 전부 구분.
					markerMaterial.color = nodeColor;
					if (markerMaterial.HasProperty("_BaseColor"))
						markerMaterial.SetColor("_BaseColor", nodeColor);
				}
			}
		}

		private void BuildEnemySpawnMarkers()
		{
			foreach (Vector3 localPosition in activeSpawnPoints)
			{
				GameObject marker = TowerDefenseVisuals.Primitive(PrimitiveType.Cube);
				marker.name = "EnemySpawnMarker";
				Collider markerCollider = marker.GetComponent<Collider>();
				if (markerCollider != null)
					Object.Destroy(markerCollider);

				marker.transform.SetParent(stageRoot, false);
				marker.transform.localPosition = localPosition.ToUnity();
				// 넓고 낮은 판 — 출현 "구역" 으로 읽히게(원반=자원과 형태로 구분).
				marker.transform.localScale = new Vector3(3f, 0.06f, 1.2f).ToUnity();

				Renderer markerRenderer = marker.GetComponent<Renderer>();
				if (markerRenderer != null)
				{
					Material markerMaterial = markerRenderer.material;
					Color spawnColor = stage.EnemyTint;
					markerMaterial.color = spawnColor;
					if (markerMaterial.HasProperty("_BaseColor"))
						markerMaterial.SetColor("_BaseColor", spawnColor);
				}
			}
		}
	}
}
