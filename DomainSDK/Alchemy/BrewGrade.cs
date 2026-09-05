namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 결과 등급. 솥 항해가 끝난 마커 상태를 목표·부작용으로 채점한 보상축.
    /// "경로가 결과를 바꾼다"의 마무리 — 질러가기(부작용↑)=조악품 / 안전 우회(부작용 0)=명품.
    /// 데이터 주도: 임계값은 BrewOutcomeRules(SO 가 감쌈), 이 enum 은 순수 등급 라벨.
    /// </summary>
    public enum BrewGrade
    {
        /// <summary>목표 효과 좌표에 도달하지 못함 = 제조 실패.</summary>
        Failed = 0,

        /// <summary>도달했으나 부작용·빗나감으로 품질 낮음 = 조악품.</summary>
        Crude = 1,

        /// <summary>도달 + 준수한 품질 = 양품.</summary>
        Fine = 2,

        /// <summary>중심 근접 + 부작용 거의 0 = 명품.</summary>
        Masterwork = 3,
    }
}
