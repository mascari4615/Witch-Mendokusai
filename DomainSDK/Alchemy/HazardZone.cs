using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 솥 지도 위 위험지대(저주 폭주 구역) = 원형 field. 데이터 주도 — 새 위험지대 = 데이터 추가만.
    /// 제조 경로가 이 원 안을 통과하면 *통과한 길이 × Severity* 만큼 부작용이 누적된다
    /// (GlassBox식 경로 적분 — 단순 in/out 플래그 X). "질러가면 빠르지만 부작용 / 돌아가면 안전".
    /// UnityEngine 의존 0(DomainSDK references=[]). 후속 HazardZoneSO(Domain)가 감싸 디자이너 노출.
    /// </summary>
    [Serializable]
    public struct HazardZone
    {
        public int Id;
        public string Name;
        public BrewVector Center;
        public float Radius;

        /// <summary>이 위험지대를 통과한 단위 거리당 누적되는 부작용 강도.</summary>
        public float SeverityPerUnit;
    }
}
