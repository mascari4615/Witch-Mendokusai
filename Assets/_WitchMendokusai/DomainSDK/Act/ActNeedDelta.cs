using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 한 행동이 욕구 하나를 얼마나 바꾸는가 (TASK-WM-408). 순수 값 타입 (DomainSDK).
    ///
    /// 부호가 곧 의미다 — <b>음수 = 소모</b>(밭을 갈면 지친다), <b>양수 = 회복</b>(자면 기운이 돈다).
    /// 「소모」와 「회복」을 두 개념으로 나누지 않는 이유: 나누면 「자는 것도 배는 고파진다」 같은
    /// 한 행동의 양방향 효과를 두 자리에 적게 되고, 그때부터 두 자리가 어긋난다.
    /// </summary>
    public readonly struct ActNeedDelta
    {
        public readonly NeedKind Kind;

        /// <summary>충족도 변화량. 음수 = 소모, 양수 = 회복.</summary>
        public readonly float Amount;

        public ActNeedDelta(NeedKind kind, float amount)
        {
            Kind = kind;
            Amount = amount;
        }

        public bool IsCost => Amount < 0f;

        public override string ToString() => $"{Kind}{(Amount >= 0f ? "+" : string.Empty)}{Amount}";
    }
}
