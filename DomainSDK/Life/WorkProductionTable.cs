using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 어떤 일이 어떤 자원을 분당 얼마나 만드는지의 단일 정본 (WorkKind → 분당 <see cref="ResourceFlow"/>[]).
    /// 비전-중립 카탈로그 — <see cref="WorkModel.Produce"/> 가 여기에 효율·시간을 곱한다.
    /// 수치는 첫 박힘(코드 상수) — INC-W7 에서 ProductionSO 로 외부화(수치 노출 룰). Idle = 생산 0.
    /// </summary>
    public static class WorkProductionTable
    {
        private static readonly IReadOnlyList<ResourceFlow> NONE = new List<ResourceFlow>();

        // 분당 기준 산출(효율 1.0) — 어떤 일이 어떤 자원을 만드는지의 정본.
        private static readonly Dictionary<WorkKind, IReadOnlyList<ResourceFlow>> FLOWS_PER_MINUTE = new()
        {
            { WorkKind.Idle, NONE },
            { WorkKind.Forage, new List<ResourceFlow> { new ResourceFlow(KnownResources.Acorn, 0.5f), new ResourceFlow(KnownResources.Herb, 0.2f) } },
            { WorkKind.Cultivate, new List<ResourceFlow> { new ResourceFlow(KnownResources.Acorn, 0.8f) } },
            { WorkKind.Mine, new List<ResourceFlow> { new ResourceFlow(KnownResources.Mineral, 0.4f) } },
            { WorkKind.Cook, new List<ResourceFlow> { new ResourceFlow(KnownResources.Food, 0.6f) } },
            { WorkKind.Craft, new List<ResourceFlow> { new ResourceFlow(KnownResources.CraftMaterial, 0.5f) } },
        };

        /// <summary>이 일의 분당 기준 산출(효율 1.0) — 미정의 일은 빈 목록(테이블 누락 시 산출 0 으로 드러남).</summary>
        public static IReadOnlyList<ResourceFlow> BaseFlowsPerMinute(WorkKind kind)
        {
            return FLOWS_PER_MINUTE.TryGetValue(kind, out IReadOnlyList<ResourceFlow> flows) ? flows : NONE;
        }
    }
}
