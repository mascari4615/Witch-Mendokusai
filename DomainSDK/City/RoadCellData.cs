using System;

namespace WitchMendokusai
{
	// 도로 셀 종류. None = sentinel (BuildingType / ContentCameraMode.None 패턴 답습).
	// Phase 1 은 Basic 한 종 — 도로 위계(저밀도/고밀도)는 직교격자 이후 확장.
	public enum RoadType
	{
		None = -1,

		Basic = 0,
	}

	// 도로 한 셀의 런타임 데이터. BuildingInstanceData 미러 — [Serializable] struct + 공개 필드 + 생성자.
	// 공개 필드 = 검증된 직렬화 경로(BuildingInstanceData 가 WorldStageSaveData 로 round-trip 중).
	// 지금은 Type 한 필드뿐 — Phase 2 (유틸 전파)에서 ServiceMask 등 첫 사용처와 함께 추가
	// (code-style § 데드 인터페이스 방지: first-use 0 필드 선제 추가 X).
	[Serializable]
	public struct RoadCellData
	{
		public RoadType Type;

		public RoadCellData(RoadType type = RoadType.Basic)
		{
			Type = type;
		}
	}
}
