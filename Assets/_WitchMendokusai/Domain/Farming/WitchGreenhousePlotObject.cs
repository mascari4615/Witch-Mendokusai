using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 마도 온실 한 칸의 게임-측 상호작용 래퍼 — 순수 GreenhousePlot 을 Fourth(플레이어) 입력에 잇는다.
	// 기존 FarmFieldObject(IInteractable, 상태별 OnInteract) 패턴 + 마도 확장: Growing 상태 상호작용 =
	// 「관찰(witness)」 = 봐줘야 진짜가 된다(IsSpecimen 자격). 테마 페이오프의 메커니즘 진입점(Phase 1f).
	//
	// 두 메커니즘 분리(judge 약점 #4 해소): 인형 Tend = 생존 안전망(살리지만 진짜로 만들진 못함) /
	// Fourth 의 Observe = 「진짜화」 고유 권능. 그래서 이 컴포넌트의 OnInteract(Growing)=관찰만이 표본을 만든다.
	//
	// 무거운 의존 0(RequireComponent X, VContainer Inject X, Awake Find X) → EditMode 직접 검증
	// (new GameObject + AddComponent + Bind/SetPlant + OnInteract). [[wm-monobehaviour-editmode-decouple]]
	//
	// plot 은 두 경로로 채워진다: ① Bind(plot) = 상위 Greenhouse 가 소유한 칸 래핑(WitchGreenhouseObject
	// 배선·자동돌봄 안전망과 공유) ② 미바인드로 OnInteract = 자기 칸 lazy 생성(독립 드롭). 어느 쪽이든 동작.
	public sealed class WitchGreenhousePlotObject : MonoBehaviour, IInteractable
	{
		// 이 칸에 심을 마도 작물. null = 런타임 기본작물(asset 없이도 동작 — WitchGreenhouseObject 와 동일 정책).
		[SerializeField] private WitchPlantSO plant;

		// 씬의 밭 칸 식별자(상위 Greenhouse 배선·디자이너 지정). 표본 이벤트의 FieldId 로 흐른다.
		[SerializeField] private int fieldId;

		private GreenhousePlot plot;

		public GreenhousePlot Plot => plot;

		public PlotPhase Phase => plot == null ? PlotPhase.Empty : plot.Phase;

		// 발행 표면(Phase 1f+ seam) — Codex 표본 박물관 / 마도서 Criteria / UI 가 구독. 초기값=NRE 방지.
		public System.Action<WitchGreenhousePlotObject> OnObserved = delegate { };
		public System.Action<HarvestResult> OnHarvested = delegate { };
		// 관찰된 개체가 영구 「진짜」로 정착하는 순간(수확 시). Codex 박물관 등록 진입점.
		public System.Action<PlantBecameSpecimenEvent> OnBecameSpecimen = delegate { };

		// 상위 Greenhouse 소유 칸에 바인드(배선 진입점). 자기 칸 생성 대신 공유 칸을 래핑 → 인형 자동돌봄
		// 안전망(Greenhouse.TickWithCarers)과 같은 칸을 가리킨다(살림 ≠ 진짜화 분리 유지).
		public void Bind(GreenhousePlot plot)
		{
			this.plot = plot;
		}

		// 칸 식별자까지 받는 배선 진입점(WitchGreenhouseObject 가 plotId 와 함께 넘김).
		public void Bind(int fieldId, GreenhousePlot plot)
		{
			this.fieldId = fieldId;
			this.plot = plot;
		}

		// 코드로 작물 교체(테스트·런타임 배선). 멱등.
		public void SetPlant(WitchPlantSO plant)
		{
			this.plant = plant;
		}

		// ★ Fourth 상호작용 — phase 별 동사. Growing 의 관찰(witness)이 테마 페이오프.
		public void OnInteract()
		{
			EnsurePlot();
			switch (plot.Phase)
			{
				case PlotPhase.Empty:    PlantHere();           break;
				case PlotPhase.Growing:  Witness();             break;
				case PlotPhase.Bloomed:  HarvestHere();         break;
				case PlotPhase.Withered: plot.ClearWithered();  break;
			}
		}

		private void EnsurePlot()
		{
			if (plot == null)
			{
				plot = new GreenhousePlot();
			}
		}

		private void PlantHere()
		{
			WitchPlantSO resolved = ResolvePlant();
			plot.Plant(resolved.ID, resolved.ToGrowthParams(), resolved.StartVitality);
		}

		// 관찰 = 진짜화 자격 부여(시들기 전 봐줘야 영구 표본). 「봐줘야 진짜가 된다」의 메커니즘.
		private void Witness()
		{
			plot.Observe();
			OnObserved.Invoke(this);
		}

		private void HarvestHere()
		{
			if (plot.TryHarvest(out HarvestResult result) == false)
			{
				return;
			}

			OnHarvested.Invoke(result);

			// 관찰된 개체만 영구 표본으로 Codex 에 「진짜」로 남는다(수확해 사라져도 증언은 남음).
			if (result.IsSpecimen)
			{
				int carerId = result.HasDominantCarer ? result.DominantCarerId : -1;
				OnBecameSpecimen.Invoke(new PlantBecameSpecimenEvent(fieldId, result.PlantDataId, carerId));
			}
		}

		private WitchPlantSO ResolvePlant()
		{
			if (plant != null)
			{
				return plant;
			}

			WitchPlantSO runtimePlant = ScriptableObject.CreateInstance<WitchPlantSO>();
			runtimePlant.ApplyDefaults();
			return runtimePlant;
		}

		// 독립 드롭(상위 틱 없음) 시 시간 진행·돌봄 — 보통은 Greenhouse 가 칸을 진행시킨다(바인드 시 호출 X).
		public void Step(int minutes)
		{
			if (plot != null)
			{
				plot.Step(minutes);
			}
		}

		public void Tend(int carerId)
		{
			if (plot != null)
			{
				plot.Tend(carerId);
			}
		}
	}
}
