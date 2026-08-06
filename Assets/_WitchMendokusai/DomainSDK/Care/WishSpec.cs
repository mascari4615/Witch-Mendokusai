using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 — 한 캐릭터의 미완의 소원 정의 (불변 데이터, DomainSDK).
    ///
    /// 소원의 *모양* (재료 모으기 + 돌봄 충족) + 완성 시 *결말 분기* 를 한 곳에 박는다. 충족 조건은
    /// 두 종류: ① <see cref="Materials"/> 가 모두 모이고 ② <see cref="SatisfactionTargets"/> 가
    /// 모두 목표치 도달. 둘 다 만족 시 소원 완성 → <see cref="OutcomeOnComplete"/> 결말 발생.
    ///
    /// ⚠ NEEDS-DECOUPLED — <see cref="SatisfactionTargets"/> 의 키는 <c>string</c> (WM-168 <c>NeedKind</c>
    /// 직접 참조 X). WM-168 통합 결정이 미커밋이므로 본 모델은 abstract 충족도 채널만 노출.
    /// 통합 시 adapter("hunger" → NeedKind.Hunger 등) 가 string ↔ NeedKind 를 잇는다.
    ///
    /// 캐릭터 id 는 본 모델에 없음 — 소원은 캐릭터의 *소유물* 이지 캐릭터를 *식별* 하지 않는다.
    /// (캐릭터→소원 매핑은 상위 레지스트리 책임 — Phase 1+ first-use)
    /// </summary>
    public sealed class WishSpec
    {
        /// <summary>이 소원의 안정 식별자(세이브/이벤트/로그용).</summary>
        public string Id { get; }

        /// <summary>구조적 분류 — UI 표현·필터링용. 코드 의미 없음.</summary>
        public WishKind Kind { get; }

        /// <summary>모아야 하는 재료들. 빈 리스트 허용(돌봄만으로 완성되는 소원).</summary>
        public IReadOnlyList<WishMaterialReq> Materials { get; }

        /// <summary>충족도 목표 (key=충족 채널 abstract 이름, value=0..1 목표). 빈 사전 허용(재료만으로 완성).</summary>
        public IReadOnlyDictionary<string, float> SatisfactionTargets { get; }

        /// <summary>완성 시 발생할 결말 분기. 데이터로 박혀 코드가 정책 결정 X (로어 deferred).</summary>
        public WishOutcome OutcomeOnComplete { get; }

        public WishSpec(
            string id,
            WishKind kind,
            IReadOnlyList<WishMaterialReq> materials,
            IReadOnlyDictionary<string, float> satisfactionTargets,
            WishOutcome outcomeOnComplete)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new System.ArgumentException("id 는 비어있을 수 없다", nameof(id));
            }

            Id = id;
            Kind = kind;
            Materials = materials ?? new List<WishMaterialReq>();
            SatisfactionTargets = satisfactionTargets ?? new Dictionary<string, float>();
            OutcomeOnComplete = outcomeOnComplete;
        }
    }
}
