using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 관계 진전 튜닝값 — 각 단계 진입에 필요한 누적 친밀도 + 자율 승급 상한. 순수 (DomainSDK).
    /// 하드코딩 금지(수치노출 룰) — 미래 SO 가 제공. (패턴: City/RciDemandCoefficients 주입)
    ///
    /// AutoCeiling 위 단계(연애·결혼)는 친밀도가 충족돼도 자율 승급 X — 4호 개입(TryIntervene)의 게이트.
    /// </summary>
    public sealed class RelationshipParams
    {
        // 단계별 진입 친밀도 — index = (int)RelationshipStage. [Stranger]=0.
        private readonly float[] stageEntryAffinity;

        /// <summary>친밀도만으로 도달 가능한 최고 단계(이 위는 4호 개입 전용).</summary>
        public RelationshipStage AutoCeiling { get; }

        public RelationshipParams(IReadOnlyList<float> stageEntryAffinity, RelationshipStage autoCeiling)
        {
            this.stageEntryAffinity = new float[stageEntryAffinity.Count];
            for (int index = 0; index < stageEntryAffinity.Count; index++)
            {
                this.stageEntryAffinity[index] = stageEntryAffinity[index];
            }

            AutoCeiling = autoCeiling;
        }

        /// <summary>해당 단계 진입에 필요한 누적 친밀도.</summary>
        public float EntryAffinityFor(RelationshipStage stage) => stageEntryAffinity[(int)stage];

        /// <summary>정의된 최고 단계(배열 마지막 인덱스).</summary>
        public RelationshipStage TopStage => (RelationshipStage)(stageEntryAffinity.Length - 1);
    }
}
