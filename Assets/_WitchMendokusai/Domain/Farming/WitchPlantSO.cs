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
		[field: Header("_" + nameof(WitchPlantSO))]
		[field: SerializeField, Min(1)] public int MinutesPerStage { get; private set; } = 60;
		[field: SerializeField, Min(1)] public int MaxStage { get; private set; } = 3;
		[field: SerializeField, Min(1f)] public float MaxVitality { get; private set; } = 100f;

		// 분당 생기 소모. 0 = 안 시듦(코지·일반작물 동등) / > 0 = 마도작물(방치 시 시듦).
		[field: SerializeField, Min(0f)] public float DrainPerMinute { get; private set; } = 1f;

		[field: SerializeField, Min(0f)] public float TendRestore { get; private set; } = 50f;

		// 시작 생기 (심을 때 PlantGrowthState 초기값). 미지정 시 MaxVitality 만큼.
		[field: SerializeField, Min(0f)] public float StartVitality { get; private set; } = 100f;

		public PlantGrowthParams ToGrowthParams()
		{
			return new PlantGrowthParams(MinutesPerStage, MaxStage, MaxVitality, DrainPerMinute, TendRestore);
		}
	}
}
