using System;

namespace WitchMendokusai.DomainSDK.Building
{
    /// <summary>
    /// TASK-WM-191 #4 건설(Like 마인크래프트 co-op) — 공유 건설 한 건물 배치 = 셀 좌표 + 건물 ID.
    /// FishNet SyncList 직렬화용 [Serializable] 구조체. Vector3Int(Unity 타입) 대신 plain int 3개
    /// = FishNet 자동 직렬화 안전(BrewStep/BrewVector 동형 — 커스텀 serializer 불요). Domain 측이
    /// new Vector3Int(CellX,CellY,CellZ) 로 GridData 좌표와 맵핑.
    /// </summary>
    [Serializable]
    public struct BuildingPlacement
    {
        public int CellX;
        public int CellY;
        public int CellZ;
        public int BuildingId;
    }
}
