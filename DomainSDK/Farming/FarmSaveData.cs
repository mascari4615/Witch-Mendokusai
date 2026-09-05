using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 밭 한 칸이 기억되는 모양 (TASK-WM-410). 순수 POCO (DomainSDK, 격상 순서: enum → SaveData).
    ///
    /// ★ 왜 「마지막으로 본 시각」을 들고 있나: 게임을 끄면 세계는 멈추지만 <b>바깥 현실은 안 멈춘다</b>.
    ///   돌아왔을 때 그 사이를 메우려면 「언제까지 자란 상태인가」를 <b>제 시계의 단위로</b> 적어 둬야 한다.
    ///   세계의 하늘을 탄 작물은 세계 분으로, 바깥 현실을 탄 작물은 유닉스 초로.
    /// </summary>
    [Serializable]
    public sealed class FarmPlotSaveData
    {
        public int X;
        public int Y;
        public int Z;

        public int PlantDataId;
        public int Clock;

        public float Vitality;
        public int GrowthMinutes;
        public bool Withered;
        public bool Observed;

        /// <summary>이 칸이 마지막으로 자란 시점 — 제 시계의 단위(세계 분 또는 유닉스 초).</summary>
        public long LastSeenStamp;

        public FarmCoord Coord => new FarmCoord(X, Y, Z);

        public PlantClock PlantClock => Clock == (int)Farming.PlantClock.Real ? Farming.PlantClock.Real : Farming.PlantClock.World;
    }

    /// <summary>밭 전체가 기억되는 모양. 순수 POCO (DomainSDK).</summary>
    [Serializable]
    public sealed class FarmSaveData
    {
        public List<FarmPlotSaveData> Plots = new();
    }
}
