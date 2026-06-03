using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 마을 주민이 할 수 있는 "일"의 종류 — 자원을 생산하는 노동. 욕구를 채우는 <see cref="ActivityKind"/> 와 직교.
    /// Idle 은 일 안 함(생산 0). 나머지는 <see cref="WorkProductionTable"/> 가 어떤 자원을 만드는지 정한다.
    /// (참조 패턴: <see cref="NeedKind"/> — enum 오름차순 결정 순회)
    /// </summary>
    public enum WorkKind
    {
        Idle = 0,
        Forage = 1,    // 채집 — 숲에서 도토리·약초 줍기
        Cultivate = 2, // 농사 — 텃밭에서 도토리(작물) 기르기
        Mine = 3,      // 채광 — 광물(마나 결정) 캐기
        Cook = 4,      // 요리 — 식량(빵·음식) 만들기
        Craft = 5,     // 가공 — 제조 재료 다듬기
    }

    /// <summary>WorkKind 결정 순회 보조 — enum 오름차순 고정(캐릭터·플랫폼 무관 동일 순서).</summary>
    public static class WorkKinds
    {
        private static readonly IReadOnlyList<WorkKind> ORDERED = new List<WorkKind>
        {
            WorkKind.Idle, WorkKind.Forage, WorkKind.Cultivate, WorkKind.Mine, WorkKind.Cook, WorkKind.Craft,
        };

        /// <summary>모든 일 종류 — enum 오름차순(테이블 완전성 검증·순회용).</summary>
        public static IReadOnlyList<WorkKind> OrderedKinds => ORDERED;
    }
}
