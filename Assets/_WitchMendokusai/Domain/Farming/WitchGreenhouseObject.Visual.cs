using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// WitchGreenhouseObject 의 보이는 것 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WitchGreenhouseObject.cs 를 본다.
	public sealed partial class WitchGreenhouseObject : MonoBehaviour
	{
		private readonly Dictionary<int, GameObject> plotVisuals = new();
		// 칸별 IInteractable 래퍼 — Fourth 가 클릭(Z키)해 관찰·수확하는 씬 진입점(plotId → 그 칸 오브젝트).
		private readonly Dictionary<int, WitchGreenhousePlotObject> plotObjects = new();
		// placeholder 큐브 색 = MaterialPropertyBlock(에디트 모드 material 인스턴스화 경고·런타임 누수 방지). URP = _BaseColor.
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private MaterialPropertyBlock colorBlock;

		// 칸별 IInteractable 오브젝트 접근(상위 배선·쿼리·테스트). 미생성(withVisuals=false)이면 null.
		public WitchGreenhousePlotObject GetPlotObject(int plotId)
		{
			return plotObjects.TryGetValue(plotId, out WitchGreenhousePlotObject plotObject) ? plotObject : null;
		}

		// 빈 칸 추가(상위 배선용 — 씬의 밭 한 칸 = 한 plotId).
		public GreenhousePlot AddPlot(int plotId)
		{
			return greenhouse.AddPlot(plotId);
		}

		// placeholder 큐브 1개 생성(한 칸) + 클릭 가능하게 배선. 실 모델 = Grey Box. 색은 RefreshVisuals 가 phase 로.
		private void SpawnPlaceholderVisual(int plotId, WitchPlantSO plant)
		{
			if (plotVisuals.ContainsKey(plotId))
			{
				return;
			}

			GameObject cube = CombatPrimitive.Create(PrimitiveType.Cube);
			cube.name = $"Plot_{plotId}";
			cube.transform.SetParent(transform, worldPositionStays: false);
			cube.transform.localPosition = new Vector3(plotId * autoPlotSpacing, 0f, 0f);

			// 상호작용은 거리 기반(InteractiveObject.GetNearest 1.5f) — 물리 충돌 불요. placeholder 콜라이더 제거.
			Collider primitiveCollider = cube.GetComponent<Collider>();
			if (primitiveCollider != null)
			{
				if (Application.isPlaying)
				{
					Destroy(primitiveCollider);
				}
				else
				{
					DestroyImmediate(primitiveCollider);
				}
			}

			WireInteractable(cube, plotId, plant);
			plotVisuals[plotId] = cube;
		}

		// 칸 GameObject 를 Fourth 클릭 대상으로 배선. WitchGreenhousePlotObject(IInteractable — phase별 동사:
		// Empty=심기/Growing=관찰/Bloomed=수확/Withered=치움) + InteractiveObject(PlayerInteraction 이 1.5f 내 탐색→OnInteract).
		// 칸과 같은 GreenhousePlot 을 공유(인형 자동돌봄과 동일 모델) — 칸 이벤트를 온실로 끌어올려 시각·표본 영구화.
		private void WireInteractable(GameObject cube, int plotId, WitchPlantSO plant)
		{
			// WitchGreenhousePlotObject 를 먼저 붙여야 InteractiveObject.Awake 의 GetComponents<IInteractable>() 가 잡는다.
			WitchGreenhousePlotObject plotObject = cube.AddComponent<WitchGreenhousePlotObject>();
			plotObject.Bind(plotId, greenhouse.GetPlot(plotId));
			plotObject.SetPlant(plant); // 수확 후 빈 칸 재심기용

			plotObject.OnObserved += _ => RefreshVisuals();
			plotObject.OnHarvested += _ => RefreshVisuals();
			plotObject.OnBecameSpecimen += specimen => HandleSpecimen(specimen.FieldId, specimen.PlantDataId);

			cube.AddComponent<InteractiveObject>();
			plotObjects[plotId] = plotObject;
		}

		// 칸 phase 로 placeholder 큐브 색 갱신(Growing 초록 / Bloomed 노랑 / Withered 갈색 / Empty 회색).
		// 시각=placeholder(사용자 비전 아님). visual 없으면(EditMode) no-op.
		private void RefreshVisuals()
		{
			if (plotVisuals.Count == 0)
			{
				return;
			}

			if (colorBlock == null)
			{
				colorBlock = new MaterialPropertyBlock();
			}

			foreach (KeyValuePair<int, GameObject> entry in plotVisuals)
			{
				GreenhousePlot plot = greenhouse.GetPlot(entry.Key);
				Renderer renderer = entry.Value == null ? null : entry.Value.GetComponent<Renderer>();
				if (plot == null || renderer == null)
				{
					continue;
				}

				Color color = ColorFor(plot.Phase, plot.Observed);
				renderer.GetPropertyBlock(colorBlock);
				colorBlock.SetColor(BASE_COLOR_ID, color);
				colorBlock.SetColor(COLOR_ID, color);
				renderer.SetPropertyBlock(colorBlock);
			}
		}

		// phase 색 + 「봐줘야 진짜」 시각: 관찰된(witnessed) 살아있는 칸은 gold 로 띄워 "이건 진짜가 됐다"를
		// 즉시 보여준다 — 개화한 관찰칸 = 밝은 금색(영구 표본), 자라는 관찰칸 = 금빛 green(증언 진행 중).
		// 안 봐준 칸은 평범한 green/yellow. 시듦/빈 칸은 관찰 무관(brown/grey).
		private static Color ColorFor(PlotPhase phase, bool observed)
		{
			switch (phase)
			{
				case PlotPhase.Growing: return observed ? new Color(0.6f, 0.85f, 0.35f) : new Color(0.4f, 0.8f, 0.4f);
				case PlotPhase.Bloomed: return observed ? new Color(1f, 0.84f, 0.25f) : new Color(0.78f, 0.7f, 0.32f);
				case PlotPhase.Withered: return new Color(0.45f, 0.32f, 0.2f);
				default: return new Color(0.6f, 0.6f, 0.6f);
			}
		}
	}
}
