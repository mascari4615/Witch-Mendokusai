namespace WitchMendokusai
{
	// 도시 존 종류 (R/C/I). Road 는 ZoneType 아님 — RoadGraph 가 별도 소유(한 셀 = zone XOR road).
	// Empty = 미지정(페인트 안 됨). DomainSDK enum (BuildingType 선례, SO 불필요).
	public enum ZoneType
	{
		Empty = 0,
		Residential = 1,
		Commercial = 2,
		Industrial = 3,
	}
}
