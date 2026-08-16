using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 행동 원장 (TASK-WM-408) — 「이 행동을 지금 할 수 있나」를 재고, 되면 <b>전부</b> 적용한다.
    /// 순수 static + 상태는 인자로 받아 변이 (참조 패턴: Life/NeedModel, Farming/WitchPlantGrowth).
    ///
    /// ★ 원장이 지키는 것 두 가지:
    ///   ① <b>전부 또는 전무.</b> 하나라도 모자라면 아무것도 안 건드린다 — 기운만 빠지고 씨앗은
    ///      안 심긴 절반의 세계를 만들지 않는다. 그래서 판정을 <b>다 끝낸 뒤에</b> 적용한다.
    ///   ② <b>선언한 것 말고는 아무 일도 안 한다.</b> 시간이 흐르는 동안의 자연 감소(배고파짐)나
    ///      성장은 여기서 안 건다 — 그건 시간을 탄 것들(<see cref="IActTimeRider"/>)의 일이다.
    ///      원장까지 그러면 같은 변화가 두 번 걸린다.
    ///
    /// 코어에 「지역」·「게임 종류」 분기는 없다. 장르는 넘겨받는 <see cref="ActSpec"/> 의 수치일 뿐이다.
    /// </summary>
    public static class ActLedger
    {
        /// <summary>
        /// 행동을 걸어 본다. 되면 true + 세계 변경, 안 되면 false + 세계 그대로(무엇이 모자랐는지는 outcome).
        /// </summary>
        public static bool TryApply(ActSpec spec, ActContext context, out ActOutcome outcome)
        {
            if (spec == null || context == null)
            {
                outcome = ActOutcome.Success(false);
                return true; // 아무것도 안 시킨 것 = 아무 일도 안 일어난 것.
            }

            if (CanAfford(spec, context, out outcome) == false)
            {
                return false;
            }

            ApplyNeeds(spec, context);
            ApplyResources(spec, context);

            bool dayChanged = AdvanceTime(spec, context);
            outcome = ActOutcome.Success(dayChanged);
            return true;
        }

        /// <summary>지금 이 행동을 감당할 수 있나 — 세계는 안 건드린다(미리보기·UI 회색 처리용).</summary>
        public static bool CanAfford(ActSpec spec, ActContext context, out ActOutcome rejection)
        {
            rejection = ActOutcome.Success(false);

            if (spec == null || context == null)
            {
                return true;
            }

            IReadOnlyList<ActNeedDelta> needDeltas = spec.NeedDeltas;
            for (int i = 0; i < needDeltas.Count; i++)
            {
                ActNeedDelta delta = needDeltas[i];
                if (delta.IsCost == false)
                {
                    continue;
                }

                if (context.HasBody == false)
                {
                    rejection = ActOutcome.NeedShort(delta.Kind);
                    return false;
                }

                if (NeedModel.CanSpend(context.Needs, delta.Kind, -delta.Amount) == false)
                {
                    rejection = ActOutcome.NeedShort(delta.Kind);
                    return false;
                }
            }

            IReadOnlyList<ActResourceDelta> resourceDeltas = spec.ResourceDeltas;
            for (int i = 0; i < resourceDeltas.Count; i++)
            {
                ActResourceDelta delta = resourceDeltas[i];
                if (delta.IsCost == false)
                {
                    continue;
                }

                if (context.Resources == null || context.Resources.AmountOf(delta.Resource) < -delta.Amount)
                {
                    rejection = ActOutcome.ResourceShort(delta.Resource);
                    return false;
                }
            }

            return true;
        }

        private static void ApplyNeeds(ActSpec spec, ActContext context)
        {
            if (context.HasBody == false)
            {
                return; // 몸에 거는 게 없으면(회복만 적힌 선언 + 몸 없음) 조용히 지나간다 — 소모는 위에서 이미 막혔다.
            }

            IReadOnlyList<ActNeedDelta> deltas = spec.NeedDeltas;
            for (int i = 0; i < deltas.Count; i++)
            {
                ActNeedDelta delta = deltas[i];
                NeedModel.Satisfy(context.Needs, context.NeedProfile, delta.Kind, delta.Amount);
            }
        }

        private static void ApplyResources(ActSpec spec, ActContext context)
        {
            if (context.Resources == null)
            {
                return;
            }

            IReadOnlyList<ActResourceDelta> deltas = spec.ResourceDeltas;
            for (int i = 0; i < deltas.Count; i++)
            {
                ActResourceDelta delta = deltas[i];
                context.Resources.Add(delta.Resource, delta.Amount);
            }
        }

        // 시간을 흘리고(하늘) 그 시간을 타는 것들을 태운다(작물·몸). 하늘이 없어도 흐름은 흐름이다.
        private static bool AdvanceTime(ActSpec spec, ActContext context)
        {
            if (spec.Minutes <= 0)
            {
                return false;
            }

            bool dayChanged = context.Calendar != null && context.Calendar.AdvanceMinutes(spec.Minutes);

            if (context.TimeRider != null)
            {
                context.TimeRider.RideMinutes(spec.Minutes, dayChanged);
            }

            return dayChanged;
        }
    }
}
