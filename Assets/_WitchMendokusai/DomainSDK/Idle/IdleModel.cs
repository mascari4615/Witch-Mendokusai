using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 방치 판의 시간 진행과 올리기 — 순수 함수 코어(결정적, EditMode 직접 테스트).
    /// (참조 패턴: Life/NeedModel — static 함수 + 상태는 인자로 받아 변이)
    ///
    /// ★ 두 가지를 보장한다:
    ///   ① <b>스텝 불변</b> — 60초를 한 번에 밟든 0.1초씩 600번 밟든 결과가 같다.
    ///      잔여 피해를 상태에 들고 있어서 성립한다. 오프라인 보상이 이 성질 위에 선다.
    ///   ② <b>결정적</b> — 무작위가 없다. 같은 입력이면 언제나 같은 표가 나온다(곡선 검증의 전제).
    /// </summary>
    public static class IdleModel
    {
        /// <summary>
        /// 몫을 셀 때 눈감아 주는 폭 — <b>부동소수점이 만든 가짜 한 마리</b>를 막는다.
        ///
        /// 실측(2026-08-16): 60초를 한 번에 밟으면 6마리, 0.1초씩 600번 밟으면 5마리가 나왔다.
        /// 0.1 을 100번 더해도 10 이 아니라 9.999999999999998 이라, 딱 체력에 닿는 순간 한 번을 못 넘고
        /// 스텝 하나가 밀린 것이다. 절단(long 캐스팅)이라 그 1e-15 짜리 오차가 그대로 <b>한 마리 차이</b>로 커진다.
        ///
        /// 체력에 비례한 폭을 준다 — 체력가 1e9 여도, 1e-3 이어도 같은 비율로 눈감는다.
        /// </summary>
        private const double COUNT_EPSILON_RATIO = 1e-9d;

        /// <summary>지금 한 방의 공격력 — 기본값 + 공격력 축이 쌓은 총량.</summary>
        public static double DamageOf(IdleState state, IdleTuning tuning)
        {
            return tuning.BaseDamage + state.Damage.TotalValue(tuning.DamageCurve);
        }

        /// <summary>지금 초당 타격 횟수 — 기본값 + 공격속도 축이 쌓은 총량.</summary>
        public static double AttackSpeedOf(IdleState state, IdleTuning tuning)
        {
            return tuning.BaseAttackSpeed + state.AttackSpeed.TotalValue(tuning.AttackSpeedCurve);
        }

        /// <summary>초당 깎는 양.</summary>
        public static double DamagePerSecond(IdleState state, IdleTuning tuning)
        {
            return DamageOf(state, tuning) * AttackSpeedOf(state, tuning);
        }

        /// <summary>초당 들어오는 자원 — 화면에 「초당 얼마」로 보여줄 값이자 곡선 판정의 축.</summary>
        public static double IncomePerSecond(IdleState state, IdleTuning tuning)
        {
            if (tuning.TargetHealth <= 0d)
            {
                return 0d;
            }

            return DamagePerSecond(state, tuning) / tuning.TargetHealth * tuning.RewardPerKill;
        }

        /// <summary>
        /// 시간을 흘린다. 깎다가 체력를 넘긴 만큼 처치로 넘어가고, 남은 피해는 다음 대상에게 이어진다.
        /// 한 스텝에 여러 대상이 쓰러질 수 있어서 나눗셈으로 한 번에 처리한다(초당 수천 마리도 같은 비용).
        /// </summary>
        public static void Step(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            double durability = tuning.TargetHealth;
            if (durability <= 0d)
            {
                return;
            }

            double dealt = state.DamageDealtToTarget + DamagePerSecond(state, tuning) * seconds;
            long felled = (long)((dealt + durability * COUNT_EPSILON_RATIO) / durability);

            if (felled > 0L)
            {
                state.Kills += felled;
                state.Resource += felled * tuning.RewardPerKill;
                dealt -= felled * durability;
            }

            state.DamageDealtToTarget = dealt;
        }

        /// <summary>모은 자원으로 한 축을 올린다. 성공하면 자원이 줄어든다.</summary>
        public static bool TryRaise(IdleState state, IdleTuning tuning, IdleUpgradeKind kind, out UpgradeRaiseFailure failure)
        {
            UpgradeLevel level = state.LevelOf(kind);
            IUpgradeCurve curve = tuning.CurveOf(kind);

            if (level.TryRaise(curve, state.Resource, out failure, out double spent) == false)
            {
                return false;
            }

            state.Resource -= spent;
            return true;
        }

        /// <summary>다음 레벨 값 — 버튼에 적을 숫자. 상한이면 false.</summary>
        public static bool TryGetNextCost(IdleState state, IdleTuning tuning, IdleUpgradeKind kind, out double cost)
        {
            return state.LevelOf(kind).TryGetNextCost(tuning.CurveOf(kind), out cost);
        }
    }
}
