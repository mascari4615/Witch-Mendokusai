namespace WitchMendokusai
{
	// 장비 데이터 없는 개발 시험 무기 (Resources/DevTrialWeapons) 의 장비 문맥 표식
	// - 장비 무기 부품은 문맥에 장비가 있다고 보고 쿨타임을 등록 (EquipmentCooldowns 는 null 이면 바로 실패)
	// - 번호는 DamageInfo.NO_DATA_ID 라 피해 기록과 자원 채집은 '장비 없음' 으로 읽음. 스킬창 칸도 없음
	public sealed class DevTrialEquipment : IItemData
	{
		public static readonly DevTrialEquipment Instance = new();

		private DevTrialEquipment()
		{
		}

		public int ID => DamageInfo.NO_DATA_ID;
		public int MaxAmount => 1;
		public ItemType Type => ItemType.Equipment;
		public ItemGrade Grade => default;
	}
}
