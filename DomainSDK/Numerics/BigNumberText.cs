using System;
using System.Globalization;

namespace WitchMendokusai.Numerics
{
    /// <summary>
    /// 큰 수를 <b>사람이 읽는 길이</b>로 줄인다 (TASK-WM-406).
    ///
    /// ★ 방치형에서 이건 장식이 아니라 <b>기능</b>이다. 며칠 돌리면 자원이 10^20 을 넘고,
    ///   그대로 찍으면 `64,389,061,132,000,000,000` 같은 줄이 화면을 넘어 <b>버튼을 밀어낸다.</b>
    ///   숫자가 커질수록 게임이 망가지는 셈이라, 표기는 진행 구조의 일부다.
    ///
    /// ★ 관례를 따른다 (생산자 클리커 계열 · 깊이 밀기 계열 · Antimatter Dimensions):
    ///   1000 단위로 <c>K M B T</c>, 그 위(10^15)부터는 <b>두 글자</b> <c>aa ab ac …</c>.
    ///   두 글자로 넘어가는 이유는 T 위의 이름(quadrillion…)이 길고 사람마다 다르게 읽어서다.
    ///
    /// ★ 엔진을 모른다 — 표현 셋(3D·2D·UI·글자)이 <b>같은 문자열</b>을 쓰게 하려고 여기 둔다.
    ///   표현마다 따로 만들면 같은 판이 창마다 다른 숫자로 보인다.
    /// </summary>
    public static class BigNumberText
    {
        private static readonly string[] SHORT_SUFFIXES = { "", "K", "M", "B", "T" };

        private const int GROUP = 1000;

        /// <summary>두 글자 이름이 시작하는 자리 — 10^15 (<c>aa</c>).</summary>
        private const int TWO_LETTER_START = 5;

        /// <summary>
        /// 사람이 읽는 꼴로. 예: <c>0</c> · <c>999</c> · <c>1.23K</c> · <c>45.6M</c> · <c>1.00aa</c>.
        /// 깨진 수(NaN·무한)는 <c>-</c> 로 — 화면에 「NaN」이 뜨는 것보다 낫다(그건 버그 신호이지 값이 아니다).
        /// </summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "-";
            }

            if (value < 0d)
            {
                return "-" + Format(-value);
            }

            if (value < GROUP)
            {
                // 1000 아래는 그대로 — 여기서 축약하면 초반 몇 분이 오히려 안 읽힌다.
                return Math.Floor(value).ToString("0", CultureInfo.InvariantCulture);
            }

            int step = 0;
            double scaled = value;

            while (scaled >= GROUP)
            {
                scaled /= GROUP;
                step++;
            }

            // ★ 나누다 보면 999.999… 가 1000 으로 반올림돼 「1000K」가 찍힌다(다음 칸이 있는데도).
            //   한 칸 올려 준다 — 그래야 표기가 <b>절대 뒤로 안 간다</b>.
            if (scaled >= 999.995d)
            {
                scaled /= GROUP;
                step++;
            }

            return scaled.ToString("0.00", CultureInfo.InvariantCulture) + SuffixOf(step);
        }

        /// <summary><paramref name="step"/> 번째 1000 묶음의 이름.</summary>
        public static string SuffixOf(int step)
        {
            if (step <= 0)
            {
                return string.Empty;
            }

            if (step < SHORT_SUFFIXES.Length)
            {
                return SHORT_SUFFIXES[step];
            }

            // 10^15 부터 aa, ab, ac … az, ba, bb …
            int index = step - TWO_LETTER_START;
            int first = index / 26;
            int second = index % 26;

            if (first > 25)
            {
                // 두 글자로도 모자란 자리 — 그 지경이면 지수로 적는 편이 정직하다.
                return "e" + (step * 3).ToString(CultureInfo.InvariantCulture);
            }

            return new string(new[] { (char)('a' + first), (char)('a' + second) });
        }
    }
}
