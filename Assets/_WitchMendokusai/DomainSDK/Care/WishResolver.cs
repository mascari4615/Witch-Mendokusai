using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Care
{
    /// <summary>
    /// TASK-WM-171 — 소원 완성 판정 + 결말 분기 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    ///
    /// 한 소원이 완성되려면 ① 재료 *모두* 충족 ② 충족도 *모두* 목표 도달. 둘 다 충족 시 완성 →
    /// <see cref="WishSpec.OutcomeOnComplete"/> 가 결말 분기. 본 클래스는 *판정* 만 — 결말 분기의
    /// *수행*(애니메이션·메모리얼 갱신·이벤트 발화) 은 Phase 1+ 상위 매니저 책임.
    ///
    /// (패턴: <see cref="WitchMendokusai.DomainSDK.Life.NeedModel"/> — 순수 static + 상태는 인자)
    ///
    /// ⚠ 의도적 미연결: WM-168 <c>NeedModel</c> 의 욕구 충족도 → 본 모델의 SatisfactionLevels 를 잇는
    /// adapter 는 Phase 1+ first-use. 본 substrate 는 두 결정(통합 여부·로어 화해) 어느 쪽으로
    /// 가도 살아남도록 *결합 0* 으로 짠다.
    /// </summary>
    public static class WishResolver
    {
        /// <summary>재료 요구가 *모두* 충족됐나 — 빈 재료 리스트는 항상 true.</summary>
        public static bool IsMaterialMet(WishSpec spec, WishProgress progress)
        {
            foreach (WishMaterialReq req in spec.Materials)
            {
                if (progress.GetMaterialCount(req.ItemId) < req.Count)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>충족도 목표가 *모두* 도달됐나 — 빈 목표는 항상 true. 목표=현재값 도달도 충족(>=).</summary>
        public static bool IsSatisfactionMet(WishSpec spec, WishProgress progress)
        {
            foreach (KeyValuePair<string, float> target in spec.SatisfactionTargets)
            {
                if (progress.GetSatisfaction(target.Key) < target.Value)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>완성 = 재료 AND 충족도 둘 다 충족. 어느 한쪽만 충족은 완성이 아님.</summary>
        public static bool IsComplete(WishSpec spec, WishProgress progress)
        {
            return IsMaterialMet(spec, progress) && IsSatisfactionMet(spec, progress);
        }

        /// <summary>
        /// 완성 시 그 소원의 결말 분기(<see cref="WishSpec.OutcomeOnComplete"/>) 반환 + true.
        /// 미완성 시 false + outcome=default. 결말 *수행* 은 호출자 책임 — 본 함수는 판정만.
        /// </summary>
        public static bool TryResolve(WishSpec spec, WishProgress progress, out WishOutcome outcome)
        {
            if (IsComplete(spec, progress) == false)
            {
                outcome = default;
                return false;
            }

            outcome = spec.OutcomeOnComplete;
            return true;
        }
    }
}
