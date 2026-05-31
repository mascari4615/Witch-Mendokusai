using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 솥 지도 항해 제조의 순수 계산 코어. MonoBehaviour 의존 0 — EditMode 테스트 first-use.
    /// 재료(BrewStep) 벡터를 누적 합성해 마커를 이동시키고, 목표 효과 좌표 도달을 판정한다.
    /// 같은 입력 = 같은 경로 = 같은 결과(결정성). Potion Craft 류 "경로가 결과를 바꾼다"의 코어.
    /// Phase 0 = 위험지대/부작용 없는 순수 벡터 항해(손맛 코어 검증). 위험장은 Phase 1.
    /// </summary>
    public static class BrewEngine
    {
        /// <summary>재료 한 step 적용 → 새 state(방향 * 갈기 만큼 이동).</summary>
        public static BrewState Apply(BrewState state, BrewStep step)
        {
            BrewVector delta = step.Direction * step.Grind;
            return new BrewState
            {
                Position = state.Position + delta,
                StepCount = state.StepCount + 1,
            };
        }

        /// <summary>재료 step 열을 시작 상태부터 순서대로 합성한 최종 state.</summary>
        public static BrewState Brew(BrewState start, IReadOnlyList<BrewStep> steps)
        {
            if (steps == null)
            {
                return start;
            }

            BrewState state = start;
            for (int i = 0; i < steps.Count; i++)
            {
                state = Apply(state, steps[i]);
            }
            return state;
        }

        /// <summary>현재 마커가 목표 효과 좌표의 허용 반경 안에 들어왔는가.</summary>
        public static bool IsReached(BrewState state, EffectTarget target)
        {
            BrewVector diff = state.Position - target.Position;
            return diff.SqrMagnitude <= target.Radius * target.Radius;
        }

        /// <summary>현재 마커에서 목표 좌표까지의 거리(근접도 표시·근접 보너스 후속 훅).</summary>
        public static float DistanceTo(BrewState state, EffectTarget target)
        {
            BrewVector diff = state.Position - target.Position;
            return diff.Magnitude;
        }
    }
}
