namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 캐릭터가 "지금 뭘 할지" 스스로 고르는 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    /// 욕구가 활동을 끌어당긴다 — 가장 급한 결핍(NeedModel.TryGetMostUrgent)을 채우는 활동을 우선.
    /// 급한 욕구가 없으면 시간대 기본(밤=자기로 예방 회복, 그 외=배회). INC-1 위에 얹히는 한 겹.
    /// (참조 패턴: Farming/WitchPlantGrowth — 순수 static + 인자 주입)
    /// </summary>
    public static class ActivitySelector
    {
        /// <summary>
        /// 지금 할 활동 — ① 급한 결핍 욕구가 있으면 그것을 채우는 활동 ② 없으면 시간대 기본.
        /// 배고프면 밤이라도 먹는다(욕구가 시간대보다 우선) — Tomodachi 자율 일상의 결.
        /// </summary>
        public static ActivityKind Select(NeedState state, NeedProfile profile, TimeOfDay timeOfDay)
        {
            if (NeedModel.TryGetMostUrgent(state, profile, out NeedKind urgent))
            {
                return ActivityForNeed(urgent);
            }

            return timeOfDay == TimeOfDay.Night ? ActivityKind.Sleep : ActivityKind.Idle;
        }

        /// <summary>
        /// 이력현상(commitment) 선택 — 지금 하는 활동을 그 욕구가 <paramref name="contentLevel"/> 에 찰 때까지 *유지*.
        /// 임계 근처 두 욕구 사이를 매 틱 깜빡이는 것(strobe)을 막아 "한 활동을 한동안 한다"는 자연스러운 리듬을 만든다.
        /// 욕구가 충분히 차면(또는 Idle 이면) <see cref="Select"/> 로 재평가. 순수 — current 만 추가 입력.
        /// </summary>
        public static ActivityKind SelectWithCommitment(NeedState state, NeedProfile profile, TimeOfDay timeOfDay, ActivityKind current, float contentLevel)
        {
            NeedKind? currentNeed = NeedForActivity(current);
            if (currentNeed.HasValue && state.Get(currentNeed.Value) < contentLevel)
            {
                return current; // 아직 충분치 않음 — 계속 그 활동(밥 다 먹기 전엔 안 일어남).
            }

            return Select(state, profile, timeOfDay);
        }

        /// <summary>한 욕구를 채우는 활동 — 미지정 욕구는 Idle 폴백.</summary>
        public static ActivityKind ActivityForNeed(NeedKind need) => need switch
        {
            NeedKind.Hunger => ActivityKind.Eat,
            NeedKind.Energy => ActivityKind.Sleep,
            NeedKind.Mood => ActivityKind.Hobby,
            NeedKind.Social => ActivityKind.Socialize,
            _ => ActivityKind.Idle,
        };

        /// <summary>
        /// 한 활동이 채우는 욕구 — <see cref="ActivityForNeed"/> 의 역. 자율 self-care(활동 수행 = 그 욕구 자가 회복)의 입력.
        /// Idle 은 채울 욕구 없음(배회·쉼) → null. 관계 도약은 활동이 아니라 4호 개입(INC-4)이라 여기 없음.
        /// </summary>
        public static NeedKind? NeedForActivity(ActivityKind activity) => activity switch
        {
            ActivityKind.Eat => NeedKind.Hunger,
            ActivityKind.Sleep => NeedKind.Energy,
            ActivityKind.Hobby => NeedKind.Mood,
            ActivityKind.Socialize => NeedKind.Social,
            _ => null,
        };
    }
}
