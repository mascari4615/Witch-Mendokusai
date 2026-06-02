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
        /// <summary>재료 한 step 적용 → 새 state(방향 * 갈기 만큼 이동). 위험지대 무시(부작용 누적 0 유지).</summary>
        public static BrewState Apply(BrewState state, BrewStep step)
        {
            BrewVector delta = step.Direction * step.Grind;
            return new BrewState
            {
                Position = state.Position + delta,
                StepCount = state.StepCount + 1,
                AccruedSideEffect = state.AccruedSideEffect,
            };
        }

        /// <summary>
        /// 재료 한 step 적용 + 이번 직선 이동이 위험지대들을 통과한 길이만큼 부작용 누적.
        /// "질러가면 빠르지만 부작용 / 돌아가면 안전" = 경로 적분(GlassBox식, 단순 in/out 플래그 X).
        /// hazards null/빈 = Apply(무위험) 와 동등.
        /// </summary>
        public static BrewState Apply(BrewState state, BrewStep step, IReadOnlyList<HazardZone> hazards)
        {
            BrewVector from = state.Position;
            BrewVector delta = step.Direction * step.Grind;
            BrewVector to = from + delta;

            float sideEffect = state.AccruedSideEffect;
            if (hazards != null)
            {
                for (int i = 0; i < hazards.Count; i++)
                {
                    float through = SegmentInCircleLength(from, to, hazards[i].Center, hazards[i].Radius);
                    sideEffect += through * hazards[i].SeverityPerUnit;
                }
            }

            return new BrewState
            {
                Position = to,
                StepCount = state.StepCount + 1,
                AccruedSideEffect = sideEffect,
            };
        }

        /// <summary>
        /// 선분 (from→to) 이 중심 center·반경 radius 원 내부를 지나는 길이. 순수 기하(결정성).
        /// 교차 없음/제로 길이 = 0. 위험지대 경로 적분의 단위 연산.
        /// </summary>
        public static float SegmentInCircleLength(BrewVector from, BrewVector to, BrewVector center, float radius)
        {
            BrewVector d = to - from;
            float a = d.SqrMagnitude;
            if (a <= 0f || radius <= 0f)
            {
                return 0f;
            }

            BrewVector f = from - center;
            float b = 2f * (f.X * d.X + f.Y * d.Y);
            float c = f.SqrMagnitude - radius * radius;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return 0f;
            }

            float sqrtDisc = (float)Math.Sqrt(discriminant);
            float t1 = (-b - sqrtDisc) / (2f * a);
            float t2 = (-b + sqrtDisc) / (2f * a);

            // 선분 범위 [0,1] 로 클램프한 교차 구간.
            float enter = t1 < 0f ? 0f : t1;
            float exit = t2 > 1f ? 1f : t2;
            if (exit <= enter)
            {
                return 0f;
            }

            float segmentLength = (float)Math.Sqrt(a);
            return (exit - enter) * segmentLength;
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

        /// <summary>
        /// 솥 항해가 끝난 마커 상태를 목표·부작용으로 채점 → 결과 등급.
        /// 미도달 = Failed(품질 0). 도달 = 강도(중심 근접 0~1, 반경 0 점목표는 도달 시 1)
        /// − 부작용 페널티(부작용 × SideEffectWeight), 0~1 clamp → 임계값으로 등급.
        /// 순수 함수(결정성) — 같은 상태·규칙 = 같은 등급.
        /// </summary>
        public static BrewOutcome Evaluate(BrewState state, EffectTarget target, BrewOutcomeRules rules)
        {
            bool reached = IsReached(state, target);
            float sideEffect = state.AccruedSideEffect;

            if (reached == false)
            {
                return new BrewOutcome
                {
                    Reached = false,
                    Potency = 0f,
                    SideEffect = sideEffect,
                    Quality = 0f,
                    Grade = BrewGrade.Failed,
                };
            }

            // 강도: 반경 안에서 중심에 가까울수록 1, 가장자리 0. 반경 0(점목표)은 도달 = 정확히 중심 = 1.
            float potency;
            if (target.Radius > 0f)
            {
                float distance = DistanceTo(state, target);
                potency = Clamp01(1f - distance / target.Radius);
            }
            else
            {
                potency = 1f;
            }

            float penalty = sideEffect * rules.SideEffectWeight;
            float quality = Clamp01(potency - penalty);

            BrewGrade grade;
            if (quality >= rules.MasterworkThreshold)
            {
                grade = BrewGrade.Masterwork;
            }
            else if (quality >= rules.FineThreshold)
            {
                grade = BrewGrade.Fine;
            }
            else
            {
                grade = BrewGrade.Crude;
            }

            return new BrewOutcome
            {
                Reached = true,
                Potency = potency,
                SideEffect = sideEffect,
                Quality = quality,
                Grade = grade,
            };
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }
    }
}
