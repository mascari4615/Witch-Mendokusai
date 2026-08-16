namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 판이 들고 다니는 주사위 (TASK-WM-406).
    ///
    /// ★ <see cref="System.Random"/> 을 안 쓴다. 이유는 취향이 아니다 —
    ///   그건 <b>저장에 담을 수가 없다</b>. 저장을 못 담으면 껐다 켤 때마다 주사위가 처음으로 돌아가고,
    ///   그러면 「끄고 켜서 다시 굴리기」가 공짜가 된다(도박이 도박이 아니게 된다).
    ///   여기서는 상태가 숫자 하나라 그대로 저장에 실린다.
    ///
    /// ★ 그리고 <b>기계가 달라도 같은 값</b>이 나온다. 표준 라이브러리는 그 보장이 없다 —
    ///   같은 저장을 다른 기계에서 열면 다른 판이 되는 것을 막는다.
    ///
    /// xorshift64* — 짧고, 상태가 64비트 하나고, 이 용도에는 충분하다.
    /// 암호에는 쓰지 마라(예측 가능하다).
    /// </summary>
    public struct IdleRandom
    {
        /// <summary>0 이면 굴러가지 않는다 — 씨앗이 없을 때 대신 쓰는 값.</summary>
        private const ulong FALLBACK_SEED = 0x9E3779B97F4A7C15UL;

        private ulong state;

        public IdleRandom(long seed)
        {
            state = seed == 0L ? FALLBACK_SEED : unchecked((ulong)seed);
        }

        /// <summary>저장에 담을 지금 상태.</summary>
        public long State => unchecked((long)state);

        /// <summary>다음 값 — 0 이상 1 미만.</summary>
        public double NextDouble()
        {
            // xorshift64*
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;

            ulong scrambled = unchecked(state * 0x2545F4914F6CDD1DUL);

            // 위쪽 53비트만 쓴다 — double 이 정확히 담을 수 있는 만큼.
            return (scrambled >> 11) / 9007199254740992d;
        }

        /// <summary><paramref name="low"/> 이상 <paramref name="high"/> 미만.</summary>
        public double NextRange(double low, double high)
        {
            if (high <= low)
            {
                return low;
            }

            return low + NextDouble() * (high - low);
        }
    }
}
