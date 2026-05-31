using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 재료 정의 = 데이터 주도. 효과 공간에서 이 재료가 미는 방향 + 기본 갈기량.
    /// "커스텀 쉽게" 의 핵심 — 새 재료 = 코드 변경 0, 이 POCO 인스턴스(후속 SO 래핑) 추가만.
    /// 직렬화 POCO(UnityEngine 의존 0) → 후속 IngredientSO(Domain, DataSO)가 감싸 디자이너 노출 + UGC/모딩 표면.
    /// Direction 은 단위 방향 의도(크기는 Grind 가 결정). 정규화는 데이터 작성자 책임(엔진은 순수 합성).
    /// </summary>
    [Serializable]
    public struct BrewIngredient
    {
        public int Id;
        public string Name;
        public BrewVector Direction;
        public float DefaultGrind;

        /// <summary>이 재료를 grind 만큼 갈았을 때의 제조 step(방향 고정 + 갈기 = 이동 거리).</summary>
        public BrewStep ToStep(float grind)
        {
            return new BrewStep
            {
                Direction = Direction,
                Grind = grind,
            };
        }

        /// <summary>기본 갈기량으로 만든 step(플레이어 입력 없을 때 디폴트).</summary>
        public BrewStep ToDefaultStep()
        {
            return ToStep(DefaultGrind);
        }
    }
}
