using System;

namespace WitchMendokusai
{
	// 존 한 셀. BuildingInstanceData / RoadCellData 미러 ([Serializable] struct + 공개 필드).
	// Density(등급)는 step6 자동성장 등급업 첫 사용처와 함께 추가 (code-style § 데드필드 방지).
	[Serializable]
	public struct ZoneCellData
	{
		public ZoneType Type;

		public ZoneCellData(ZoneType type)
		{
			Type = type;
		}
	}
}
