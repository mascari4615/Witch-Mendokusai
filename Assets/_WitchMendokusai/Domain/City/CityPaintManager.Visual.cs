using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using VContainer;

namespace WitchMendokusai
{
	// CityPaintManager 의 보이는 것 만들기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 CityPaintManager.cs 를 본다.
	public partial class CityPaintManager : MonoBehaviour
	{
		// 셀 → 시각 큐브 (ZoneGrid/RoadGraph 가 데이터 진실, 이건 그 투영 = 렌더 캐시).
		private readonly Dictionary<Vector3Int, GameObject> cellVisuals = new();
		// 자동 성장한 건물 시각 큐브 — **projection only (렌더 캐시)**. 집계/성장/쇠퇴 판정의 진실은
		// GridData(CityCellQuery 경유). (구조 리뷰: 시각 캐시를 진실로 쓰면 save/load 후 갈라짐.)
		private readonly Dictionary<Vector3Int, GameObject> buildingVisuals = new();
		private readonly Dictionary<Color, Material> materialCache = new();
		private Material templateMaterial;
		private Transform visualRoot;
		// 발전소 시각 마커 (PowerSourceRegistry 가 데이터 진실, 이건 투영).
		private readonly Dictionary<Vector3Int, GameObject> powerSourceVisuals = new();

		private Color ZoneColor(ZoneType type)
		{
			switch (type)
			{
				case ZoneType.Residential: return residentialColor;
				case ZoneType.Commercial: return commercialColor;
				case ZoneType.Industrial: return industrialColor;
				default: return Color.gray;
			}
		}

		// 존/도로 타일 (납작한 판).
		private void SetCellVisual(Vector3Int cell, Color color)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, cellTileHeight);
				cellVisuals[cell] = visual;
			}

			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
		}

		// 자동 성장 건물 (높은 큐브) — 존 타일 위에.
		private void SetBuildingVisual(Vector3Int cell, Color color)
		{
			bool created = false;
			if (buildingVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, buildingHeight);
				visual.name = $"Bldg_{cell.x}_{cell.y}";
				buildingVisuals[cell] = visual;
				created = true;
			}

			// 건물은 존 색을 어둡게 (타일과 구분).
			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(color * 0.6f);

			// 신규 spawn 만 솟아오름 연출 (매일 re-color 마다 튀면 X — 함정).
			if (created)
				AnimateBuildingRise(visual);
		}

		// INC-8 (WM-180) — 건물 성장 연출: 바닥에 납작하게 시작 → full 높이로 솟아오름(OutBack 팝). scale.y·pos.y 를
		// 같은 ease/duration 으로 동시 트윈 → pos.y = scale.y/2 유지로 밑면 y=0 고정(공중 부양 X).
		private void AnimateBuildingRise(GameObject cube)
		{
			Transform tr = cube.transform;
			float fullScaleY = tr.localScale.y; // = buildingHeight
			float fullPosY = tr.position.y;     // = groundY + buildingHeight*0.5 (밑면이 지면 위)
			float startScaleY = fullScaleY * 0.02f; // 거의 납작하게 시작
			// TASK-WM-181 INC-1 — 밑면을 지면(groundY)에 고정. 평탄 0 가정 폐기 → 깊이 있는 월드에서 지면 위 솟음.
			float groundY = fullPosY - fullScaleY * 0.5f;

			Vector3 scale = tr.localScale;
			tr.localScale = new Vector3(scale.x, startScaleY, scale.z);
			Vector3 pos = tr.position;
			tr.position = new Vector3(pos.x, groundY + startScaleY * 0.5f, pos.z);

			tr.DOKill();
			tr.DOScaleY(fullScaleY, buildingRiseDuration).SetEase(buildingRiseEase);
			tr.DOMoveY(fullPosY, buildingRiseDuration).SetEase(buildingRiseEase);
		}

		// INC-8 (WM-180) — 건물 쇠퇴 연출: 가라앉으며 납작해진 뒤 Destroy. dict 즉시 제거(재성장 시 새 큐브).
		// 자동 쇠퇴 전용 (유저 erase 는 ClearBuildingVisual 즉시 — 별도). OnComplete 전 파괴되면 null 가드.
		private void AnimateBuildingSink(Vector3Int cell)
		{
			if (buildingVisuals.TryGetValue(cell, out GameObject visual) == false)
				return;

			buildingVisuals.Remove(cell);

			Transform tr = visual.transform;
			float sinkScaleY = tr.localScale.y * 0.02f;

			tr.DOKill();
			Sequence sink = DOTween.Sequence();
			sink.Join(tr.DOScaleY(sinkScaleY, buildingSinkDuration).SetEase(buildingSinkEase));
			sink.Join(tr.DOMoveY(sinkScaleY * 0.5f, buildingSinkDuration).SetEase(buildingSinkEase));
			sink.OnComplete(() =>
			{
				if (visual != null)
					Destroy(visual);
			});
		}

		// 셀 좌표에 큐브 1개 생성 (height = Y 크기·바닥에서 띄움). Grid 회전 상속.
		private GameObject CreateCellCube(Vector3Int cell, float height)
		{
			GameObject cube = CombatPrimitive.Create(PrimitiveType.Cube);
			cube.transform.SetParent(visualRoot, false);
			cube.name = $"Cell_{cell.x}_{cell.y}";

			cube.layer = IGNORE_RAYCAST_LAYER; // 클릭 평면판정 무방해
			Collider cubeCollider = cube.GetComponent<Collider>();
			if (cubeCollider != null)
				Destroy(cubeCollider);

			Vector3 buildPos = buildManager.GetWorldPosition(cell).ToUnity();
			// TASK-WM-181 INC-1 — buildPos.y = 실제 지면(GroundProbe). 밑면을 지면에 올리고 height/2 만큼 띄움(평탄 0 폐기).
			cube.transform.position = new Vector3(buildPos.x, buildPos.y + height * 0.5f, buildPos.z);
			cube.transform.rotation = buildManager.Grid.transform.rotation; // 다이아몬드 칸 정합
			Vector3 cellSize = buildManager.Grid.cellSize;
			cube.transform.localScale = new Vector3(cellSize.x * cellTileScale, height, cellSize.y * cellTileScale);
			return cube;
		}

		private void ClearCellVisual(Vector3Int cell)
		{
			if (cellVisuals.TryGetValue(cell, out GameObject visual))
			{
				cellVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private void ClearBuildingVisual(Vector3Int cell)
		{
			if (buildingVisuals.TryGetValue(cell, out GameObject visual))
			{
				buildingVisuals.Remove(cell);
				visual.transform.DOKill(); // 진행 중 성장 연출 트윈 정리 후 즉시 파괴 (유저 erase = 무연출).
				Destroy(visual);
			}
		}

		// 발전소 시각 마커 (건물보다 높은 노란 큐브 — placeholder, 실 prefab/스킨=마법진? deferred).
		private void SetPowerSourceVisual(Vector3Int cell)
		{
			if (powerSourceVisuals.TryGetValue(cell, out GameObject visual) == false)
			{
				visual = CreateCellCube(cell, buildingHeight * 1.5f);
				visual.name = $"PowerSource_{cell.x}_{cell.y}";
				powerSourceVisuals[cell] = visual;
			}

			visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(powerSourceColor);
		}

		private void ClearPowerSourceVisual(Vector3Int cell)
		{
			if (powerSourceVisuals.TryGetValue(cell, out GameObject visual))
			{
				powerSourceVisuals.Remove(cell);
				Destroy(visual);
			}
		}

		private Material GetMaterial(Color color)
		{
			if (materialCache.TryGetValue(color, out Material material))
				return material;

			Material created = new(templateMaterial);
			created.color = color;
			if (created.HasProperty("_BaseColor"))
				created.SetColor("_BaseColor", color);

			materialCache[color] = created;
			return created;
		}
	}
}
