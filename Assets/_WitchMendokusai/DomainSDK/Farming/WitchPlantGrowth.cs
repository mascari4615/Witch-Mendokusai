using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 마도 작물 성장·돌봄·시듦의 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    /// 기존 단조 시간성장의 일반화 — 레거시(Drain=0)는 시듦 없는 degenerate case.
    /// engine-free (System.Math) — root DomainSDK asmdef 순수성 유지.
    ///
    /// 비전 매핑: Step=시간(돌봄 안 하면 생기 소모→시듦) / Tend=돌봄(생기 회복) /
    /// Observed=플레이어 관찰→IsSpecimen(진짜화) / TryGetDominantCarer=변이 입력("누가 돌봤나").
    /// </summary>
    public static class WitchPlantGrowth
    {
        /// <summary>
        /// 시간 경과 한 스텝. 생기를 소모하고, 살아있으면 성장을 적립한다.
        /// 생기가 0 이하로 떨어지면 시들고 그 스텝의 성장은 적립하지 않는다(죽은 식물 안 자람).
        /// </summary>
        public static void Step(PlantGrowthState state, PlantGrowthParams parameters, int minutes)
        {
            if (state.Withered)
            {
                return;
            }

            state.Vitality -= parameters.DrainPerMinute * minutes;

            if (state.Vitality <= 0f)
            {
                state.Vitality = 0f;
                state.Withered = true;
                return;
            }

            state.GrowthMinutes += minutes;
        }

        /// <summary>돌봄 1회 — 생기를 회복하고(상한 클램프) 돌봄자를 기록(변이 입력). 시든 식물엔 무효.</summary>
        public static void Tend(PlantGrowthState state, PlantGrowthParams parameters, int carerId)
        {
            if (state.Withered)
            {
                return;
            }

            state.Vitality = Math.Min(parameters.MaxVitality, state.Vitality + parameters.TendRestore);
            state.RecordTend(carerId);
        }

        /// <summary>현재 성장 단계 (누적 분 / 단계당 분, 최종 단계 상한).</summary>
        public static int StageOf(PlantGrowthState state, PlantGrowthParams parameters)
        {
            if (parameters.MinutesPerStage <= 0)
            {
                return parameters.MaxStage;
            }

            return Math.Min(state.GrowthMinutes / parameters.MinutesPerStage, parameters.MaxStage);
        }

        /// <summary>개화·수확 가능 — 안 시들고 최종 단계 도달.</summary>
        public static bool IsHarvestable(PlantGrowthState state, PlantGrowthParams parameters)
        {
            return state.Withered == false && StageOf(state, parameters) >= parameters.MaxStage;
        }

        /// <summary>「진짜화」 — 안 시들고, 관찰됐고, 개화에 도달한 개체만 영구 표본 자격.</summary>
        public static bool IsSpecimen(PlantGrowthState state, PlantGrowthParams parameters)
        {
            return state.Withered == false && state.Observed && StageOf(state, parameters) >= parameters.MaxStage;
        }

        /// <summary>가장 많이 돌본 돌봄자(변이 입력). 동률 = 최저 id 타이브레이크(결정성). 돌봄 0 = false.</summary>
        public static bool TryGetDominantCarer(PlantGrowthState state, out int carerId)
        {
            carerId = -1;
            int bestCount = 0;

            foreach (KeyValuePair<int, int> entry in state.TendCounts)
            {
                bool outright = entry.Value > bestCount;
                bool tieLowerId = entry.Value == bestCount && carerId != -1 && entry.Key < carerId;

                if (outright || tieLowerId)
                {
                    bestCount = entry.Value;
                    carerId = entry.Key;
                }
            }

            return carerId != -1;
        }
    }
}
