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

        /// <summary>
        /// 몇 칸을 깔고 앉나 — <b>세계가 정한 크기</b> (TASK-WM-217).
        ///
        /// ★ 왜 여기 실리나: 세계는 크기를 알고 보내는데(스냅샷 w·l) 이 자료형이 그걸 버렸다.
        ///   그래서 화면은 크기를 자기 자산으로 짐작했고, 「세계에 선 것」을 pivot 한 칸으로만 알았다.
        ///   결과: 2×2 를 세우면 나머지 3칸이 「세계에 없는 것」이 되어 즉시 지워졌다 —
        ///   사람 눈에는 여러 칸 건물이 <b>한 칸으로 접히는</b> 것으로 보인다.
        ///   0 이면 예전 값(한 칸)으로 읽는다 — 옛 통로도 그대로 돈다.
        /// </summary>
        public int Width;

        public int Length;

        /// <summary>한 칸짜리로 읽어도 안전한 크기 — 0·음수는 1로 본다.</summary>
        public int WidthOrOne => Width < 1 ? 1 : Width;

        public int LengthOrOne => Length < 1 ? 1 : Length;
    }
}
