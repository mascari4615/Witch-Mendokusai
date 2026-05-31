namespace WitchMendokusai.DomainSDK.Farming
{
    // 마도 온실 이벤트 (QuestEvents 컨벤션 = 평범한 record, marker interface 없음).
    // 상위(FarmFieldObject/온실 MonoBehaviour)가 발행 → UI 게이지·Codex 표본 박물관·마도서 Criteria 가 구독.

    // 작물이 개화(최종 단계 도달)했다.
    public record PlantBloomedEvent(int FieldId, int PlantDataId);

    // 작물이 돌봄 부족으로 시들었다.
    public record PlantWitheredEvent(int FieldId, int PlantDataId);

    // 관찰된 작물이 영구 표본으로 「진짜」가 됐다 — DominantCarerId 가 변이를 가른다.
    public record PlantBecameSpecimenEvent(int FieldId, int PlantDataId, int DominantCarerId);
}
