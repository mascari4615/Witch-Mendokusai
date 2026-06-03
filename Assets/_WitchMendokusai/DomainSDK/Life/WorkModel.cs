using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 노동 산출 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    /// <see cref="NeedModel"/> 가 욕구만 변이하듯, WorkModel 은 부작용 없이 "이만큼 생산했다"를 ResourceFlow[]로 반환.
    /// 재고 적용(<see cref="CityEconomy.AddStock"/>)은 Domain 레이어(LifeAgent)가 — DomainSDK 는 Unity·게임상태를 모름.
    /// </summary>
    public static class WorkModel
    {
        private static readonly IReadOnlyList<ResourceFlow> EMPTY = new List<ResourceFlow>();

        /// <summary>
        /// <paramref name="minutes"/> 분 동안 <paramref name="kind"/> 일을 해서 생산한 자원 —
        /// 분당 기준 산출(<see cref="WorkProductionTable"/>) × 효율(<see cref="WorkProfile.EfficiencyOf"/>) × 분.
        /// 반환 <see cref="ResourceFlow.Rate"/> = 이 기간 실제 생산량(rate 아님). Idle·0분·미정의 = 빈 목록.
        /// </summary>
        public static IReadOnlyList<ResourceFlow> Produce(WorkKind kind, WorkProfile profile, int minutes)
        {
            IReadOnlyList<ResourceFlow> baseFlows = WorkProductionTable.BaseFlowsPerMinute(kind);
            if (baseFlows.Count == 0 || minutes <= 0)
            {
                return EMPTY;
            }

            float efficiency = profile.EfficiencyOf(kind);
            List<ResourceFlow> produced = new List<ResourceFlow>(baseFlows.Count);

            foreach (ResourceFlow baseFlow in baseFlows)
            {
                float amount = baseFlow.Rate * efficiency * minutes;
                produced.Add(new ResourceFlow(baseFlow.Resource, amount));
            }

            return produced;
        }
    }
}
