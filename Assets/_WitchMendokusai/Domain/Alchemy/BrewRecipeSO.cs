using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-3 — 솥 지도 레시피/마도서 페이지 정의 (디자이너/모딩/UGC 노출).
    /// 한 제조 판 = 목표 효과좌표(Target) + 위험지대(Hazards) + 고를 재료(Ingredients) 한 묶음.
    /// DomainSDK POCO 를 Domain DataSO 로 감쌈 (6-동기: 미래·모딩·UGC). 새 레시피 = 이 에셋 추가만(코드 0).
    /// `WM > Alchemy > BrewRecipeSO` 우클릭 Create. ID/Name(=효과명) = DataSO 베이스.
    ///
    /// TASK-WM-183 INC-W7 — <see cref="MaterialCosts"/> = 이 제조가 마을 창고(CityEconomy)에서 덜어 쓰는 벌크 재료.
    /// "주민 노동 → CityEconomy 적재 → 욘 제조 소비" 루프의 비용 정의(수치 노출). 비우면 무료 제조(소비 0).
    /// 재료 항해 벡터(Ingredients/효과공간)와 벌크 비용(MaterialCosts/경제)은 직교 — 전자=손맛, 후자=자원 고리.
    /// </summary>
    [CreateAssetMenu(fileName = "BR_", menuName = "WM/Alchemy/" + nameof(BrewRecipeSO))]
    public class BrewRecipeSO : DataSO
    {
        [field: Header("_" + nameof(BrewRecipeSO))]
        [PropertyOrder(10)][field: SerializeField] public EffectTarget Target { get; private set; }
        [PropertyOrder(11)][field: SerializeField] public List<HazardZone> Hazards { get; private set; } = new List<HazardZone>();
        [PropertyOrder(12)][field: SerializeField] public List<BrewIngredientSO> Ingredients { get; private set; } = new List<BrewIngredientSO>();

        [field: Header("마을 벌크 재료 비용 (제조 시 CityEconomy 에서 소비) — INC-W7")]
        [PropertyOrder(15)][field: SerializeField] public List<BrewMaterialCost> MaterialCosts { get; private set; } = new List<BrewMaterialCost>();

        [field: Header("보상 (제조 완료 시)")]
        [PropertyOrder(20)][field: SerializeField] public ItemData ResultItem { get; private set; }
        [PropertyOrder(21)][field: SerializeField] public int BaseAmount { get; private set; } = 1;

        /// <summary>SO → DomainSDK 런타임 레시피 POCO 변환 (EffectName = DataSO.Name).</summary>
        public BrewRecipe ToRecipe()
        {
            return new BrewRecipe
            {
                Id = ID,
                EffectName = Name,
                Target = Target,
            };
        }

        /// <summary>
        /// 인스펙터 벌크 비용(int resourceId + float amount) → DomainSDK <see cref="ResourceFlow"/>[] 런타임 변환.
        /// <see cref="BrewConsumptionModel"/> 가 이 목록으로 CityEconomy 재고를 확인·차감(factory 책임, ToRecipe 형제).
        /// </summary>
        public IReadOnlyList<ResourceFlow> ToMaterialCosts()
        {
            List<ResourceFlow> costs = new List<ResourceFlow>(MaterialCosts.Count);
            foreach (BrewMaterialCost cost in MaterialCosts)
            {
                costs.Add(cost.ToResourceFlow());
            }
            return costs;
        }
    }

    /// <summary>
    /// 솥 제조 한 판이 마을 창고에서 소비하는 벌크 재료 한 줄 — 인스펙터 노출용(자원 id + 소요량).
    /// <see cref="ResourceId"/>(readonly struct) 는 인스펙터 직렬화 회피라(CityEconomySaveData 동형) 저장은 int,
    /// <see cref="ToResourceFlow"/> 가 런타임 ResourceFlow 로 변환. resourceId 값 = <see cref="DomainSDK.Life.KnownResources"/> 대역.
    /// </summary>
    [Serializable]
    public struct BrewMaterialCost
    {
        [SerializeField] private int resourceId;
        [SerializeField] private float amount;

        public ResourceFlow ToResourceFlow()
        {
            return new ResourceFlow(new ResourceId(resourceId), amount);
        }
    }
}
