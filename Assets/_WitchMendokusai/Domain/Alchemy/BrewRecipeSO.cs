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
    /// </summary>
    [CreateAssetMenu(fileName = "BR_", menuName = "WM/Alchemy/" + nameof(BrewRecipeSO))]
    public class BrewRecipeSO : DataSO
    {
        [field: Header("_" + nameof(BrewRecipeSO))]
        [PropertyOrder(10)][field: SerializeField] public EffectTarget Target { get; private set; }
        [PropertyOrder(11)][field: SerializeField] public List<HazardZone> Hazards { get; private set; } = new List<HazardZone>();
        [PropertyOrder(12)][field: SerializeField] public List<BrewIngredientSO> Ingredients { get; private set; } = new List<BrewIngredientSO>();

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
    }
}
