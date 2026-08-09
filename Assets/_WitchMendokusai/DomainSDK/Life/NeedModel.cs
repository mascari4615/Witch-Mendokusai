using WitchMendokusai.Numerics;

namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 욕구 충족도의 시간 경과·회복·문제 판정 순수 함수 코어 (DomainSDK, 결정적, EditMode 직접 테스트).
    /// 캐릭터가 가만 있으면 욕구가 줄고(Step), 활동·개입으로 채워지고(Satisfy), 임계 아래면 문제(IsInNeed).
    /// 가장 시급한 결핍(TryGetMostUrgent)이 INC-2 활동 선택의 입력이 된다.
    /// (참조 패턴: Farming/WitchPlantGrowth — 순수 static + 상태는 인자로 받아 변이)
    /// </summary>
    public static class NeedModel
    {
        // 충족도 하한 — 굶주려도 음수로 안 감. 타입 범위 상수(튜닝값 아님).
        private const float NEED_FLOOR = 0f;

        /// <summary>시간 경과 한 스텝 — 프로필의 모든 욕구를 분당 속도만큼 소모(하한 0 클램프).</summary>
        public static void Step(NeedState state, NeedProfile profile, int minutes)
        {
            foreach (NeedKind kind in profile.Kinds)
            {
                NeedSpec spec = profile.SpecOf(kind);
                float next = state.Get(kind) - spec.DecayPerMinute * minutes;
                state.Set(kind, Mathf.Clamp(next, NEED_FLOOR, spec.Max));
            }
        }

        /// <summary>한 욕구를 채운다(활동·개입) — 상한 클램프. INC-4 가 의미(아이템·돌봄)를 입힌다.</summary>
        public static void Satisfy(NeedState state, NeedProfile profile, NeedKind kind, float amount)
        {
            NeedSpec spec = profile.SpecOf(kind);
            state.Set(kind, Mathf.Clamp(state.Get(kind) + amount, NEED_FLOOR, spec.Max));
        }

        /// <summary>이 욕구가 문제 상태인가 — 충족도가 임계 미만.</summary>
        public static bool IsInNeed(NeedState state, NeedProfile profile, NeedKind kind)
        {
            return state.Get(kind) < profile.SpecOf(kind).LowThreshold;
        }

        /// <summary>
        /// 가장 시급한 결핍 욕구 — 임계 미만인 것 중 정규화 충족도(현재/상한)가 가장 낮은 것.
        /// 동률은 NeedKind enum 최저값 타이브레이크(결정성). 문제 욕구가 하나도 없으면 false.
        /// 욕구마다 상한이 달라도 공정하게 비교되도록 절대값이 아니라 정규화 비율로 판정.
        /// </summary>
        public static bool TryGetMostUrgent(NeedState state, NeedProfile profile, out NeedKind urgent)
        {
            urgent = default;
            bool found = false;
            float lowestRatio = float.MaxValue;

            foreach (NeedKind kind in profile.Kinds)
            {
                if (IsInNeed(state, profile, kind) == false)
                {
                    continue;
                }

                NeedSpec spec = profile.SpecOf(kind);
                float ratio = spec.Max > 0f ? state.Get(kind) / spec.Max : 0f;

                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    urgent = kind;
                    found = true;
                }
            }

            return found;
        }
    }
}
