using System;

namespace WitchMendokusai
{
	// 발전소(전력원) 한 셀의 영속 데이터 — 전파 range(홉). [Serializable] struct + 공개 필드(RoadCellData/
	// ZoneCellData/CitizenSaveData 미러 = 검증된 직렬화 경로). 비전-중립: 발전소가 마법진/제단/화로인지는
	// 스킨 deferred, 데이터는 셀+range 만. (셀 좌표는 dict 키 = PowerSourceRegistry 가 보유.)
	[Serializable]
	public struct PowerSourceData
	{
		public int Range;

		public PowerSourceData(int range)
		{
			Range = range;
		}
	}
}
