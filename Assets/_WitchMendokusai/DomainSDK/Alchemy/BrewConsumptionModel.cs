using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// TASK-WM-183 INC-W7 — 브루잉 ↔ 마을 경제 루프 닫기. 욘의 솥 제조가 소비하는 마을 벌크 재료의 순수 판정·차감 코어.
    /// <see cref="WorkModel"/> 의 거울: 노동이 <see cref="CityEconomy"/> 에 자원을 *쌓고*(생산), 제조가 *덜어낸다*(소비).
    /// "주민 노동 → 마을 창고(CityEconomy) → 욘 제조 소비" 고리의 소비 끝단.
    ///
    /// CityEconomy 는 순수 DomainSDK 원장(Unity 의존 0)이라 이 모델이 직접 다뤄도 DomainSDK references=[] 불변 —
    /// "확인(GetStock) 후 충분하면 차감(AddStock 음수)" 의 원자성/비-부분차감을 한곳에 묶어 EditMode 직접 검증.
    /// 비용 = <see cref="ResourceFlow"/>[] (자원 id + 소요량) — ProductionRecipe.Inputs 와 동형 단위(다입력 지원).
    ///
    /// 이중차감 회피: 소비는 *이 모델의 Consume 한 번* 만 — 포션(이산, ItemInventory)과 벌크 재료(CityEconomy)는
    /// 별개 원장이라(TASK 결정표) 벌크 차감 + 포션 산출은 중복계상 아님(루프 닫기). 호출자는 Consume 성공 시에만 산출.
    /// </summary>
    public static class BrewConsumptionModel
    {
        /// <summary>
        /// <paramref name="economy"/> 가 <paramref name="costs"/> 전부를 *지금* 댈 수 있는가(각 자원 재고 ≥ 소요량).
        /// costs null/빈 = 무료(true). 같은 자원이 여러 줄이면 소요량 합산해 비교(중복 키 안전).
        /// </summary>
        public static bool CanAfford(CityEconomy economy, IReadOnlyList<ResourceFlow> costs)
        {
            if (economy == null || costs == null || costs.Count == 0)
            {
                return true;
            }

            Dictionary<ResourceId, float> required = Aggregate(costs);
            foreach (KeyValuePair<ResourceId, float> entry in required)
            {
                if (economy.GetStock(entry.Key) < entry.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 충분하면 <paramref name="costs"/> 만큼 <paramref name="economy"/> 재고를 차감하고 true, 부족하면 *아무것도 건드리지 않고* false.
        /// 원자적(check-all → deduct-all): 부분차감 없음 = 한 자원만 부족해도 다른 자원이 새지 않음.
        /// costs null/빈 = 무료 제조(true, 차감 0). 소요량 0/음수 줄은 무시(잘못된 비용이 재고를 늘리지 않게).
        /// </summary>
        public static bool Consume(CityEconomy economy, IReadOnlyList<ResourceFlow> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return true;
            }

            if (economy == null)
            {
                return false;
            }

            Dictionary<ResourceId, float> required = Aggregate(costs);
            foreach (KeyValuePair<ResourceId, float> entry in required)
            {
                if (economy.GetStock(entry.Key) < entry.Value)
                {
                    return false;
                }
            }

            foreach (KeyValuePair<ResourceId, float> entry in required)
            {
                economy.AddStock(entry.Key, -entry.Value);
            }

            return true;
        }

        // 같은 자원이 여러 줄로 와도(다입력 레시피 작성 실수·모딩) 소요량을 합산 — 확인/차감이 일관되게.
        // 소요량 ≤ 0 줄은 제외(음수 비용 = 재고 증가 버그 차단).
        private static Dictionary<ResourceId, float> Aggregate(IReadOnlyList<ResourceFlow> costs)
        {
            Dictionary<ResourceId, float> required = new Dictionary<ResourceId, float>();
            foreach (ResourceFlow cost in costs)
            {
                if (cost.Rate <= 0f)
                {
                    continue;
                }

                required[cost.Resource] = (required.TryGetValue(cost.Resource, out float amount) ? amount : 0f) + cost.Rate;
            }

            return required;
        }
    }
}
