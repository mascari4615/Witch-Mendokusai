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

        /// <summary>한 욕구를 채우는 활동 — 미지정 욕구는 Idle 폴백.</summary>
        public static ActivityKind ActivityForNeed(NeedKind need) => need switch
        {
            NeedKind.Hunger => ActivityKind.Eat,
            NeedKind.Energy => ActivityKind.Sleep,
            NeedKind.Mood => ActivityKind.Hobby,
            NeedKind.Social => ActivityKind.Socialize,
            _ => ActivityKind.Idle,
        };
    }
}
