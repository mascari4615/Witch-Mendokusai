using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 인형(carer) 별 변이 수확물 — 가장 많이 돌본 인형(DominantCarerId = Doll.ID)이 결과를 가른다.
	// 링=거칠게 야생 / 알리사=질서롭게 균일 (테마: 누가 길렀나가 수확물을 바꾼다). 비어 있으면 변이 없음(기본 HarvestLoots).
	[System.Serializable]
	public struct CarerLoot
	{
		[SerializeField] private int carerId;
		[SerializeField] private ItemData item;

		public int CarerId => carerId;
		public ItemData Item => item;

		public CarerLoot(int carerId, ItemData item)
		{
			this.carerId = carerId;
			this.item = item;
		}
	}

	// 마도 작물 정의 SO — 디자이너(욘)가 Inspector 로 성장·돌봄·시듦 수치를 튜닝(수치노출 룰=MDD).
	// DataSO(ID/Name/Sprite) 상속 + DomainSDK 순수 성장모델(PlantGrowthParams)의 게임 데이터 producer.
	// 기존 SeedItemData(일반 농사, 단조 GrowSeconds)와 별개 = 마도 작물 전용(절충 톤: 시듦 있음).
	//
	// 작물 *종류*(.asset 인스턴스, 스프라이트, 디제틱 이름)는 사용자 Grey Box. 본 클래스 = 골격만.
	[CreateAssetMenu(fileName = nameof(WitchPlantSO), menuName = "WM/Farming/" + nameof(WitchPlantSO))]
	public sealed class WitchPlantSO : DataSO
	{
		private const int DEFAULT_MINUTES_PER_STAGE = 60;
		private const int DEFAULT_MAX_STAGE = 3;
		private const float DEFAULT_MAX_VITALITY = 100f;
		private const float DEFAULT_DRAIN_PER_MINUTE = 1f;
		private const float DEFAULT_TEND_RESTORE = 50f;
		private const float DEFAULT_START_VITALITY = 100f;

		[field: Header("_" + nameof(WitchPlantSO))]
		[field: SerializeField, Min(1)] public int MinutesPerStage { get; private set; } = DEFAULT_MINUTES_PER_STAGE;
		[field: SerializeField, Min(1)] public int MaxStage { get; private set; } = DEFAULT_MAX_STAGE;
		[field: SerializeField, Min(1f)] public float MaxVitality { get; private set; } = DEFAULT_MAX_VITALITY;

		// 분당 생기 소모. 0 = 안 시듦(코지·일반작물 동등) / > 0 = 마도작물(방치 시 시듦).
		[field: SerializeField, Min(0f)] public float DrainPerMinute { get; private set; } = DEFAULT_DRAIN_PER_MINUTE;

		[field: SerializeField, Min(0f)] public float TendRestore { get; private set; } = DEFAULT_TEND_RESTORE;

		// 시작 생기 (심을 때 PlantGrowthState 초기값).
		[field: SerializeField, Min(0f)] public float StartVitality { get; private set; } = DEFAULT_START_VITALITY;

		[field: Header("_" + nameof(WitchPlantSO) + " 수확")]
		// 기본 수확물 추첨표(확률) — SeedItemData.HarvestLoots 패턴 재사용. DataSO = ItemData. 비어 있으면 수확물 0.
		[field: SerializeField] public List<DataSOWithPercentage> HarvestLoots { get; private set; } = new();
		// 변이 수확물 — DominantCarer 가 여기 있으면 기본 추첨 대신 이 아이템(누가 길렀나 = 결과 변이).
		[field: SerializeField] public List<CarerLoot> CarerLoots { get; private set; } = new();

		// Unity 가 asset 생성·우클릭 Reset 시 호출 — CreateInstance 는 [field: SerializeField] 이니셜라이저를
		// 직렬화 디폴트(0)로 덮으므로(EditMode 실측), 디자이너가 빈 마도작물 만들 때 0=즉시 시듦 버그 방지.
		private void Reset()
		{
			ApplyDefaults();
		}

		// 빈 인스턴스에 합리적 기본값 주입(디자이너 안전망). Reset 콜백 본체 + 테스트 진입점.
		public void ApplyDefaults()
		{
			MinutesPerStage = DEFAULT_MINUTES_PER_STAGE;
			MaxStage = DEFAULT_MAX_STAGE;
			MaxVitality = DEFAULT_MAX_VITALITY;
			DrainPerMinute = DEFAULT_DRAIN_PER_MINUTE;
			TendRestore = DEFAULT_TEND_RESTORE;
			StartVitality = DEFAULT_START_VITALITY;
		}

		public PlantGrowthParams ToGrowthParams()
		{
			return new PlantGrowthParams(MinutesPerStage, MaxStage, MaxVitality, DrainPerMinute, TendRestore);
		}

		// 수확물 결정 — 변이 우선: 가장 많이 돌본 인형(dominantCarer)이 CarerLoots 에 있으면 그 변이품, 없으면 기본 추첨.
		// HarvestLoots 도 비었으면 null(수확물 없음 = 호출자가 인벤토리 추가 skip).
		public ItemData ResolveHarvestItem(bool hasDominantCarer, int dominantCarerId)
		{
			ItemData variant = ResolveCarerVariant(CarerLoots, hasDominantCarer, dominantCarerId);
			return variant != null ? variant : DrawDefaultLoot();
		}

		// 변이 해석(순수·결정적 — EditMode 직접 테스트). carer 매치 없으면 null.
		public static ItemData ResolveCarerVariant(IReadOnlyList<CarerLoot> carerLoots, bool hasDominantCarer, int dominantCarerId)
		{
			if (hasDominantCarer == false || carerLoots == null)
			{
				return null;
			}

			foreach (CarerLoot carerLoot in carerLoots)
			{
				if (carerLoot.CarerId == dominantCarerId && carerLoot.Item != null)
				{
					return carerLoot.Item;
				}
			}

			return null;
		}

		// 기본 수확물 확률 추첨(Probability<ItemData>). 표 비었으면 null.
		private ItemData DrawDefaultLoot()
		{
			if (HarvestLoots == null || HarvestLoots.Count == 0)
			{
				return null;
			}

			Probability<ItemData> probability = new();
			foreach (DataSOWithPercentage loot in HarvestLoots)
			{
				if (loot.DataSO is ItemData itemData)
				{
					probability.Add(itemData, loot.Percentage);
				}
			}

			return probability.Get();
		}

#if UNITY_EDITOR
		// 에디터 데이터 시드 전용(WitchPlantSeedTool). 정본 입력 = 디자이너 인스펙터 — 이건 샘플 종 자동 구성용.
		public void EditorSetHarvestLoots(List<DataSOWithPercentage> loots) => HarvestLoots = loots;
		public void EditorSetCarerLoots(List<CarerLoot> carerLoots) => CarerLoots = carerLoots;
#endif
	}
}
