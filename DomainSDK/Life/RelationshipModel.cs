using WitchMendokusai.Numerics;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 관계 친밀도 누적·단계 승급·개입 게이트의 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    ///
    /// 핵심 invariant (TASK-WM-168): 친밀도 자율 상승은 AutoCeiling(동거)까지만 단계를 올린다.
    /// 그 위(연애·결혼)는 AddAffinity 로 절대 도달 못 하고, 4호 개입 TryIntervene 으로만 — 친밀도 충족 시.
    /// (참조 패턴: Farming/WitchPlantGrowth — 순수 static + 인자 변이)
    /// </summary>
    public static class RelationshipModel
    {
        private const float AFFINITY_FLOOR = 0f;

        /// <summary>
        /// 친밀도를 더하고(하한 0) 자율 승급 — 친밀도가 닿는 최고 단계까지 올리되 AutoCeiling 을 넘지 않는다.
        /// amount 음수(다툼)면 친밀도는 줄지만 이미 오른 단계는 내리지 않는다(관계는 쉽게 후퇴 안 함).
        /// </summary>
        public static void AddAffinity(RelationshipState state, RelationshipParams parameters, float amount)
        {
            state.Affinity = Mathf.Max(AFFINITY_FLOOR, state.Affinity + amount);

            RelationshipStage best = state.Stage;
            RelationshipStage limit = AutoAdvanceLimit(parameters);

            for (RelationshipStage candidate = state.Stage + 1; candidate <= limit; candidate++)
            {
                if (state.Affinity < parameters.EntryAffinityFor(candidate))
                {
                    break;
                }

                best = candidate;
            }

            state.Stage = best;
        }

        /// <summary>
        /// 4호(플레이어) 개입으로 한 단계 승급 — AutoCeiling 위(연애·결혼) 게이트 전용.
        /// ① 다음 단계가 자율 영역이면(AddAffinity 가 처리) 개입 대상 아님 → false
        /// ② 친밀도가 다음 단계 진입값 미만이면 false ③ 이미 최고 단계면 false.
        /// 셋 다 통과해야 승급. = "연애·결혼은 4호 개입으로만, 그것도 충분히 친할 때만".
        /// </summary>
        public static bool TryIntervene(RelationshipState state, RelationshipParams parameters)
        {
            RelationshipStage next = state.Stage + 1;

            if (next > parameters.TopStage)
            {
                return false;
            }

            if ((int)next <= (int)parameters.AutoCeiling)
            {
                return false;
            }

            if (state.Affinity < parameters.EntryAffinityFor(next))
            {
                return false;
            }

            state.Stage = next;
            return true;
        }

        /// <summary>다음 단계가 4호 개입을 요구하는가(자율 승급 불가 영역인가).</summary>
        public static bool RequiresIntervention(RelationshipState state, RelationshipParams parameters)
        {
            RelationshipStage next = state.Stage + 1;
            return next <= parameters.TopStage && (int)next > (int)parameters.AutoCeiling;
        }

        // 자율 승급이 닿을 수 있는 최고 단계 — ceiling 과 정의된 최고 단계 중 작은 쪽.
        private static RelationshipStage AutoAdvanceLimit(RelationshipParams parameters)
        {
            return (int)parameters.AutoCeiling < (int)parameters.TopStage
                ? parameters.AutoCeiling
                : parameters.TopStage;
        }
    }
}
