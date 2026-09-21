using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleSession.cs 의 Views 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 성장 줄 미리보기 계산.
    public sealed partial class IdleSession
    {
        /// <summary>감정 버튼 한 줄에 필요한 값과 차단 사유.</summary>
        public IdleAppraiseView ViewAppraisal(int tier)
        {
            return new IdleAppraiseView(
                IdleGear.AppraiseCost(tier, tuning),
                IdlePotentials.WhyNot(state, tuning, tier));
        }

        private bool CanRaiseAnyStat(int dollId)
        {
            for (int stat = 0; stat <= (int)IdleUpgradeKind.Recovery; stat++)
            {
                if (IdleModel.TryGetCost(state, tuning, dollId, (IdleUpgradeKind)stat, 1, out double cost)
                    && state.Resource >= cost)
                {
                    return true;
                }
            }

            return false;
        }

        public IdleUpgradeView ViewDollStat(int dollId, IdleUpgradeKind kind, int amount)
        {
            int index = state.IndexOfDoll(dollId);
            if (index < 0)
            {
                return new IdleUpgradeView(kind, 0, 0d, 0d, true, false, 0d, 0d);
            }

            IdleDollOwned owned = state.Dolls[index];
            int level = owned.StatLevel(kind);
            bool hasNext = IdleModel.TryGetCost(state, tuning, dollId, kind, amount, out double nextCost);

            return new IdleUpgradeView(
                kind,
                level,
                DollStatValue(dollId, kind),
                nextCost,
                hasNext == false,
                hasNext && state.Resource >= nextCost,
                ValueAfterRaising(dollId, kind, amount),
                SecondsToAfford(nextCost, hasNext));
        }

        /// <summary>
        /// 한 단계 올린 <b>뒤의</b> 값 — 실제로 올려 보고 되돌린다.
        ///
        /// ★ 공식을 화면이나 여기서 다시 쓰지 않는다. 두 번 쓰면 언젠가 갈리고,
        ///   그러면 <b>버튼이 거짓말</b>을 한다(사면 다른 값이 나온다).
        /// </summary>
        private double ValueAfterRaising(int dollId, IdleUpgradeKind kind, int amount)
        {
            int index = state.IndexOfDoll(dollId);
            if (index < 0)
            {
                return 0d;
            }

            IdleDollOwned before = state.Dolls[index];
            IdleDollOwned afterOwned = before;
            afterOwned.SetStatLevel(kind, before.StatLevel(kind) + amount);
            state.Dolls[index] = afterOwned;
            double after = DollStatValue(dollId, kind);
            state.Dolls[index] = before;

            return after;
        }

        private double DollStatValue(int dollId, IdleUpgradeKind kind)
        {
            switch (kind)
            {
                case IdleUpgradeKind.Damage:
                    return IdleModel.DamageOfDoll(state, tuning, dollId);
                case IdleUpgradeKind.AttackSpeed:
                    return IdleModel.AttackSpeedOfDoll(state, tuning, dollId);
                case IdleUpgradeKind.MaxHealth:
                    return IdleSquad.MaxHealthOfDoll(state, tuning, dollId);
                case IdleUpgradeKind.Defense:
                    double defense = IdleDolls.DefenseOf(state, tuning, dollId);
                    return 1d - 1d / (1d + defense);
                case IdleUpgradeKind.CriticalChance:
                    return IdleDolls.CriticalChanceOf(state, tuning, dollId);
                case IdleUpgradeKind.CriticalDamage:
                    return IdleDolls.CriticalDamageOf(state, tuning, dollId);
                default:
                    return IdleDolls.HealPerKillShareOf(state, tuning, dollId);
            }
        }

        /// <summary>
        /// 이 생산자를 하나 더 사면 <b>초당 수입이 몇 배</b>가 되나.
        ///
        /// ★ 공식을 화면이 다시 쓰지 않게. 두 번 쓰면 언젠가 갈리고 버튼이 거짓말을 한다.
        ///
        /// ★ 배수(장비·인형·도감·폭주)는 사도 안 사도 <b>똑같이</b> 곱해져 비율에서 지워진다 —
        ///   그래서 바닥(<see cref="IdleBase.RawOutputPerSecond"/>)만으로 잰다. 값은 전과 같다.
        ///
        /// ⚠ 전에는 <b>생산자를 하나 얹었다 되돌리며</b> 쟀다. 조회하는 자리가 판을 건드리면,
        ///   그 사이에 무슨 일이 나는 순간 공짜 생산자가 남는다 — 그런 자리는 안 만드는 게 낫다.
        ///   덤으로 훑기가 두 번에서 한 번이 된다(화면이 매 프레임 생산자마다 부른다).
        /// </summary>
        private double IncomeGainOf(int kind)
        {
            double before = IdleBase.RawOutputPerSecond(state, tuning);

            if (before <= 0d)
            {
                return double.PositiveInfinity;
            }

            return (before + IdleBase.OutputOf(kind, tuning)) / before;
        }

        /// <summary>지금 벌이로 이 값을 모으는 데 걸리는 시간(초).</summary>
        private double SecondsToAfford(double cost, bool hasNext)
        {
            if (hasNext == false || state.Resource >= cost)
            {
                return 0d;
            }

            double perSecond = IdleModel.IncomePerSecond(state, tuning);
            if (perSecond <= 0d)
            {
                return double.PositiveInfinity;
            }

            return (cost - state.Resource) / perSecond;
        }
    }
}

