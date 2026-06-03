using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-3 — 솥 지도 재료 정의 (디자이너/모딩/UGC 노출).
    /// DomainSDK POCO `BrewIngredient`(references=[]) 를 Domain DataSO 로 감싼다 (6-동기: 미래·모딩·UGC).
    /// 새 재료 = 이 SO 에셋 추가만 (코드 변경 0). ID/Name = DataSO 베이스. Direction/DefaultGrind = 수치 노출.
    /// `WM > Alchemy > BrewIngredientSO` 우클릭 Create. ToRuntime() 가 런타임 POCO 로 변환(factory 책임).
    /// </summary>
    [CreateAssetMenu(fileName = "BI_", menuName = "WM/Alchemy/" + nameof(BrewIngredientSO))]
    public class BrewIngredientSO : DataSO
    {
        [field: Header("_" + nameof(BrewIngredientSO))]
        [PropertyOrder(10)][field: SerializeField] public BrewVector Direction { get; private set; }
        [PropertyOrder(11)][field: SerializeField] public float DefaultGrind { get; private set; } = 1f;

        /// <summary>SO → DomainSDK 런타임 POCO 변환 (Domain factory 책임, WitchMendokusai/CLAUDE.md § DomainSDK 격상).</summary>
        public BrewIngredient ToRuntime()
        {
            return new BrewIngredient
            {
                Id = ID,
                Name = Name,
                Direction = Direction,
                DefaultGrind = DefaultGrind,
            };
        }
    }
}
