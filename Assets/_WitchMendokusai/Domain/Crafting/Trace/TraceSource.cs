using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	// 도구 번호에서 자취로
	public static class TraceSource
	{
		public static TraceType OfTool(int equipmentDataId)
		{
			if (equipmentDataId == DamageInfo.NO_DATA_ID)
				return TraceType.None;
			EquipmentData equipment = SOHelper.Get<ItemData>(equipmentDataId) as EquipmentData;
			return equipment != null ? equipment.Trace : TraceType.None;
		}
	}
}
