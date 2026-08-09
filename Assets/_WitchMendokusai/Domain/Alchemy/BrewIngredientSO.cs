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

        /// <summary>
        /// 이 재료가 <b>가방의 어느 아이템</b>인가 (TASK-WM-217).
        /// 세계는 재료를 가방에서 실제로 꺼내 넣는다 — 그래서 아이템과 이어져 있어야 넣을 수 있다.
        /// 비워 두면(null) 이 재료는 세계의 솥에 못 들어간다(혼자 노는 화면에서만 뜻이 있다).
        /// </summary>
        [PropertyOrder(12)][field: SerializeField] public ItemData Item { get; private set; }

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
