namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 4호 개입을 욕구·관계 모델에 적용하는 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    /// 자율 레이어(INC-1~3) 위에 "플레이어의 손길"을 얹는다 — 욕구를 채워주고, 중재하고, 큰 인연을 맺어준다.
    /// Bond(연애·결혼)는 RelationshipModel.TryIntervene 게이트를 그대로 통과 — 자율로는 절대 못 가는 영역.
    /// </summary>
    public static class InterventionModel
    {
        /// <summary>욕구 해소 개입(Feed/Rest/Cheer/Socialize)이 채우는 욕구 — 그 외(Mediate/Bond)는 false.</summary>
        public static bool TryGetReliefNeed(InterventionKind intervention, out NeedKind need)
        {
            switch (intervention)
            {
                case InterventionKind.Feed:
                    need = NeedKind.Hunger;
                    return true;
                case InterventionKind.Rest:
                    need = NeedKind.Energy;
                    return true;
                case InterventionKind.Cheer:
                    need = NeedKind.Mood;
                    return true;
                case InterventionKind.Socialize:
                    need = NeedKind.Social;
                    return true;
                default:
                    need = default;
                    return false;
            }
        }

        /// <summary>한 욕구를 채우는 개입 종류.</summary>
        public static InterventionKind ReliefForNeed(NeedKind need) => need switch
        {
            NeedKind.Hunger => InterventionKind.Feed,
            NeedKind.Energy => InterventionKind.Rest,
            NeedKind.Mood => InterventionKind.Cheer,
            NeedKind.Social => InterventionKind.Socialize,
            _ => InterventionKind.Feed,
        };

        /// <summary>욕구 해소 개입 적용 — 해당 욕구를 채운다. 욕구 해소형이 아니면 무효(false).</summary>
        public static bool ApplyRelief(NeedState state, NeedProfile profile, InterventionKind intervention, float amount)
        {
            if (TryGetReliefNeed(intervention, out NeedKind need) == false)
            {
                return false;
            }

            NeedModel.Satisfy(state, profile, need, amount);
            return true;
        }

        /// <summary>지금 이 캐릭터에게 권할 해소 개입 — 가장 급한 결핍 욕구에 맞는 것. 문제 없으면 false.</summary>
        public static bool TryGetSuggestedRelief(NeedState state, NeedProfile profile, out InterventionKind suggested)
        {
            if (NeedModel.TryGetMostUrgent(state, profile, out NeedKind urgent))
            {
                suggested = ReliefForNeed(urgent);
                return true;
            }

            suggested = default;
            return false;
        }

        /// <summary>지금 이 관계를 Bond(연애·결혼 도약)할 수 있는가 — 게이트 도달 + 친밀도 충족.</summary>
        public static bool CanBond(RelationshipState state, RelationshipParams parameters)
        {
            return RelationshipModel.RequiresIntervention(state, parameters)
                && state.Affinity >= parameters.EntryAffinityFor(state.Stage + 1);
        }

        /// <summary>4호가 두 캐릭터를 맺어준다 — 한 단계 도약(연애→결혼). 게이트·친밀도 미충족이면 false.</summary>
        public static bool Bond(RelationshipState state, RelationshipParams parameters)
        {
            return RelationshipModel.TryIntervene(state, parameters);
        }

        /// <summary>4호가 다툰 둘을 중재 — 친밀도를 회복시킨다(단계 후퇴는 없음).</summary>
        public static void Mediate(RelationshipState state, RelationshipParams parameters, float restore)
        {
            RelationshipModel.AddAffinity(state, parameters, restore);
        }
    }
}
