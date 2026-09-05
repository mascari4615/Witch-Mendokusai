using System;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 두 캐릭터 사이 관계의 런타임 상태 — 누적 친밀도 + 현재 단계. 순수 POCO (DomainSDK).
    /// RelationshipModel 이 진행시킨다. 미래(INC-5)에 LifeAgentSaveData 가 쌍 목록으로 흡수 예정.
    /// </summary>
    [Serializable]
    public sealed class RelationshipState
    {
        /// <summary>관계의 두 당사자(캐릭터 id). 순서는 의미 없음 — 호출자가 쌍 키로 관리.</summary>
        public int CharacterA;
        public int CharacterB;

        /// <summary>누적 친밀도(0 이상). 단계 승급의 원천.</summary>
        public float Affinity;

        /// <summary>현재 관계 단계.</summary>
        public RelationshipStage Stage;

        public RelationshipState()
        {
        }

        public RelationshipState(int characterA, int characterB)
        {
            CharacterA = characterA;
            CharacterB = characterB;
            Stage = RelationshipStage.Stranger;
        }
    }
}
