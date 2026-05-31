namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 마도 작물의 성장·돌봄·시듦 튜닝값. FarmPlantData(SO) 가 ToGrowthParams() 로 제공 예정
    /// (수치노출 룰 — 하드코딩 X). 순수 값 타입 (DomainSDK).
    ///
    /// 레거시 호환 = degenerate case: DrainPerMinute = 0 → 생기 안 줄어듦 → 절대 안 시듦
    /// = 기존 FarmFieldObject 의 단조 시간성장 그대로. 마도 작물만 Drain > 0 으로 시듦 활성.
    /// </summary>
    public readonly struct PlantGrowthParams
    {
        /// <summary>한 성장 단계에 필요한 (살아있는) 분.</summary>
        public readonly int MinutesPerStage;

        /// <summary>최종 성장 단계 (도달 시 개화·수확 가능).</summary>
        public readonly int MaxStage;

        /// <summary>생기 상한 (돌봄으로 채워지는 최대).</summary>
        public readonly float MaxVitality;

        /// <summary>분당 생기 소모량. 0 = 절대 안 시듦(레거시).</summary>
        public readonly float DrainPerMinute;

        /// <summary>돌봄 1회당 생기 회복량.</summary>
        public readonly float TendRestore;

        public PlantGrowthParams(int minutesPerStage, int maxStage, float maxVitality, float drainPerMinute, float tendRestore)
        {
            MinutesPerStage = minutesPerStage;
            MaxStage = maxStage;
            MaxVitality = maxVitality;
            DrainPerMinute = drainPerMinute;
            TendRestore = tendRestore;
        }
    }
}
