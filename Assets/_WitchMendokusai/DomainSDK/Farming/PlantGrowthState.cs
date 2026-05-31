using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Farming
{
    /// <summary>
    /// 마도 작물 한 그루의 런타임 상태. 순수 POCO (DomainSDK) — WitchPlantGrowth 가 진행시킨다.
    /// FarmRuntimeData 가 Phase 1 에서 이 필드들을 흡수 예정 (지금은 모델 first-use 격리).
    ///
    /// Observed = 시들기 전 Fourth(플레이어)가 관찰했는가 = 「진짜화(영구 표본)」 자격의 핵심.
    /// 돌봄자별 횟수 = 변이 입력 ("누가 가장 돌봤나" → 같은 씨앗 다른 재료).
    /// </summary>
    [Serializable]
    public sealed class PlantGrowthState
    {
        /// <summary>현재 생기. 0 도달 → 시듦.</summary>
        public float Vitality;

        /// <summary>누적된 살아있는 분 (성장 단계의 원천). 시든 틱은 적립 안 됨.</summary>
        public int GrowthMinutes;

        /// <summary>시들었는가. true = 성장 정지·수확 불가.</summary>
        public bool Withered;

        /// <summary>시들기 전 플레이어가 관찰했는가 (영구 표본 자격).</summary>
        public bool Observed;

        private readonly Dictionary<int, int> tendCountBySource = new();

        /// <summary>돌봄자 id → 돌본 횟수 (변이 결정 입력).</summary>
        public IReadOnlyDictionary<int, int> TendCounts => tendCountBySource;

        public PlantGrowthState()
        {
        }

        public PlantGrowthState(float startVitality)
        {
            Vitality = startVitality;
        }

        public void RecordTend(int carerId)
        {
            tendCountBySource.TryGetValue(carerId, out int count);
            tendCountBySource[carerId] = count + 1;
        }
    }
}
