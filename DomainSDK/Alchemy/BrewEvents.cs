namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-4 — 솥 지도 제조 완료 이벤트 (QuestEvents/GardenEvents 컨벤션 = 평범한 record, marker interface 없음).
    /// 항해를 "완성"하면 발행 → 마도서 진행/도감/통계/연출 리스너가 구독. DomainSDK 순수(references=[]).
    /// ResultItemId = 생산된 포션 아이템 ID(-1 = 보상 없음/미설정), Amount = 등급에 따른 생산량(0 = 실패).
    /// </summary>
    public record PotionBrewedEvent(int RecipeId, BrewGrade Grade, float Potency, float SideEffect, int ResultItemId, int Amount);
}
