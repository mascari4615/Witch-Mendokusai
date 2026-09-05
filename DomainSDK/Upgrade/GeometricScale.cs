using System;

namespace WitchMendokusai.DomainSDK.Upgrade
{
    /// <summary>
    /// 「단계마다 몇 배」 — 한 값이 단계를 따라 커지는 꼴 하나 (SSOT).
    ///
    /// ★ <see cref="IUpgradeCurve"/> 와 다르다. 저쪽은 <b>사는 것</b>(값과 비용이 같이 오른다),
    ///   이쪽은 <b>주어지는 것</b>(몇 번째 단계인가로만 정해진다). 둘을 한 타입에 뭉치면
    ///   「비용 없는 곡선」 같은 뜻 없는 상태가 생긴다.
    ///
    /// ★ 방치형·클리커의 뼈대가 사실상 이 한 줄이다 — 적 체력도, 보상도, 값도 전부
    ///   「단계마다 몇 배」다. 그래서 게임마다 다시 안 짜도 되게 여기에 둔다.
    /// </summary>
    public readonly struct GeometricScale
    {
        /// <summary>0단계의 값.</summary>
        public double Base { get; }

        /// <summary>한 단계 갈 때마다 곱해지는 배수.</summary>
        public double Ratio { get; }

        public GeometricScale(double baseValue, double ratio)
        {
            Base = baseValue;
            Ratio = ratio;
        }

        /// <summary><paramref name="step"/> 단계(0부터)의 값.</summary>
        public double At(int step)
        {
            if (step <= 0)
            {
                return Base;
            }

            return Base * Math.Pow(Ratio, step);
        }

        /// <summary>
        /// 값이 <paramref name="target"/> 이상이 되는 첫 단계 — 「어디서 벽에 부딪히나」를 되묻는 쪽.
        /// 배수가 1 이하면 영영 안 닿으므로 <c>false</c>.
        /// </summary>
        public bool TryStepReaching(double target, out int step)
        {
            step = 0;

            if (Base <= 0d || Ratio <= 1d)
            {
                return Base >= target;
            }

            if (Base >= target)
            {
                return true;
            }

            step = (int)Math.Ceiling(Math.Log(target / Base) / Math.Log(Ratio));
            return true;
        }
    }
}
