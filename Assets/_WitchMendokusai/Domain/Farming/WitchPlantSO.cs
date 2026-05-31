using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
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
	}
}
