using System;
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
    public static partial class IdleModel
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

        /// <summary>가장 좋은 잠재가 주는 배수.</summary>
        public static double PotentialMultiplier(IdleState state)
        {
            return 1d + state.BestPotentialValue;
        }

        private static double DamageRoot(IdleState state, IdleTuning tuning)
        {
            return tuning.BaseDamage
                * PrestigeMultiplier(state, tuning)
                * PotentialMultiplier(state)
                * IdleGear.DamageMultiplier(state, tuning)
                * IdleHeroes.AxisMultiplierOf(state, tuning, IdleHeroAxis.Damage)
                * IdleHeroes.DiscoveryMultiplierOf(state, tuning);
        }

        /// <summary>영웅 한 명의 한 방. 공격력과 치명타 기대값이 그 영웅의 성장에서 나옴</summary>
        public static double DamageOfHero(IdleState state, IdleTuning tuning, int heroId)
        {
            double stat = IdleHeroes.StatValueOf(state, tuning, heroId, IdleUpgradeKind.Damage);
            return (DamageRoot(state, tuning) + stat)
                * IdleHeroes.ExpectedCriticalMultiplierOf(state, tuning, heroId);
        }

        /// <summary>부대 평균 한 방. 오프라인과 요약 화면용</summary>
        public static double DamageOf(IdleState state, IdleTuning tuning)
        {
            double total = 0d;
            int count = 0;

            for (int seat = 0; seat < state.Party.Length && seat < IdleHeroes.MAIN_SLOTS; seat++)
            {
                if (state.Party[seat] < 0)
                {
                    continue;
                }

                total += DamageOfHero(state, tuning, state.Party[seat]);
                count++;
            }

            return count > 0 ? total / count : DamageOfHero(state, tuning, IdleHeroes.STARTER_ID);
        }

        /// <summary>영웅 한 명의 초당 타격 횟수</summary>
        public static double AttackSpeedOfHero(IdleState state, IdleTuning tuning, int heroId)
        {
            return (tuning.BaseAttackSpeed
                + IdleHeroes.StatValueOf(state, tuning, heroId, IdleUpgradeKind.AttackSpeed))
                * IdleGear.SpeedMultiplier(state, tuning)
                * IdleHeroes.AxisMultiplierOf(state, tuning, IdleHeroAxis.Speed)
                * IdleSurge.Multiplier(state, tuning);
        }

        /// <summary>부대 평균 초당 타격 횟수. 오프라인과 요약 화면용</summary>
        public static double AttackSpeedOf(IdleState state, IdleTuning tuning)
        {
            double total = 0d;
            int count = 0;

            for (int seat = 0; seat < state.Party.Length && seat < IdleHeroes.MAIN_SLOTS; seat++)
            {
                if (state.Party[seat] < 0)
                {
                    continue;
                }

                total += AttackSpeedOfHero(state, tuning, state.Party[seat]);
                count++;
            }

            double average = count > 0
                ? total / count
                : AttackSpeedOfHero(state, tuning, IdleHeroes.STARTER_ID);
            return average * IdleSquad.FightingShare(state);
        }

        /// <summary>초당 깎는 양.</summary>
        public static double DamagePerSecond(IdleState state, IdleTuning tuning)
        {
            return DamageOf(state, tuning) * AttackSpeedOf(state, tuning);
        }

        /// <summary>지금 단계의 대상 체력.</summary>
        public static double TargetHealthOf(IdleState state, IdleTuning tuning)
        {
            return TargetHealthAt(state.Stage, tuning);
        }

        /// <summary>그 단계 대상의 체력 — <b>지금 서 있는 자리와 무관하게</b> 묻는다.</summary>
        public static double TargetHealthAt(int stage, IdleTuning tuning)
        {
            return tuning.TargetHealthByStage.At(stage - 1);
        }

        /// <summary>지금 단계의 처치 보상.</summary>
        public static double RewardOf(IdleState state, IdleTuning tuning)
        {
            return tuning.RewardByStage.At(state.Stage - 1);
        }

        /// <summary>초당 들어오는 자원 — <b>기지가 내는 것</b>이다(잡기는 장비를 낸다).</summary>
        public static double IncomePerSecond(IdleState state, IdleTuning tuning)
        {
            return IdleBase.OutputPerSecond(state, tuning);
        }

        /// <summary>
        /// 한 스텝에서 넘어갈 수 있는 단계 수의 상한 — <b>멈추지 않는 판</b>을 막는 안전선.
        ///
        /// 체력 배수가 1 이하로 맞춰지면(손잡이는 사람이 돌린다) 아무리 내려가도 벽이 안 생겨
        /// 이 반복이 끝나지 않는다. 게임이 그냥 멎어 버리는 것보다 여기서 잘리는 편이 낫다.
        /// 정상 설정에서는 닿지 않는다 — 배수 1.55 면 8시간치 피해로도 수십 단계다.
        /// </summary>
        private const int MAX_STAGES_PER_STEP = 4096;

        /// <summary>
        /// 시간을 흘린다. 깎다가 체력을 넘긴 만큼 처치로 넘어가고, 남은 피해는 다음 대상에게 이어진다.
        /// 한 스텝에 여러 대상이 쓰러질 수 있어서 나눗셈으로 한 번에 처리한다(초당 수천 마리도 같은 비용).
        ///
        /// ★ <b>단계 경계에서 한 번 끊는다.</b> 단계가 바뀌면 체력도 보상도 바뀌므로, 경계를 무시하고
        ///   한 번에 나누면 다음 단계 몫을 이전 단계 값으로 쳐준다 — 60초를 한 번에 밟을 때와
        ///   0.1초씩 600번 밟을 때가 갈리는 자리이기도 하다(스텝 불변이 여기서 깨진다).
        /// </summary>
        /// <summary>
        /// 한 방에 필요한 <b>때리는 횟수</b> — 넘치는 피해는 버린다.
        ///
        /// ★ 이게 없으면 <b>한 번 때려 여러 마리가 죽는다.</b> 실측(2026-08-16):
        ///   머무르기를 넣자 6단계에서 6시간에 떨어진 것이 <b>1.1e17개</b>가 됐다 —
        ///   체력은 고정인데 공격력이 계속 올라 처치 속도가 발산했고, 그게 자원 발산으로,
        ///   다시 업그레이드 발산으로 돌았다. <b>머무르기가 벽을 통째로 없앤 것이다.</b>
        ///
        /// ★ 넘치는 피해를 버리면 처치 속도가 <b>공격 속도 위로 못 간다</b>.
        ///   공격 속도 곡선은 공격력보다 훨씬 완만하므로 파밍이 유용하되 무한하지 않다.
        ///   현실에도 맞는다 — 한 대 때려 한 마리가 죽지, 열 마리가 죽지 않는다.
        /// </summary>
        /// ★ <b>`long` 으로 돌려주면 안 된다</b> (실측 2026-08-16).
        ///   단계 298 의 체력은 3.4e57 이고 그때 공격력은 1e27 언저리다 — 필요 타격 수가 <b>3.4e30</b>,
        ///   `long` 최대(9.2e18)를 훌쩍 넘는다. `(long)` 변환은 그 경우 <b>정의되지 않은 값</b>(보통 음수)을
        ///   내고, 그 음수로 나눈 처치 수가 쓰레기가 된다.
        ///   이레 시뮬이 판마다 「+1단계」로 멎던 것이 균형 문제인 줄 알았는데 <b>고장이었다.</b>
        ///   `double` 이면 절벽이 없다 — 아주 큰 값은 그냥 「못 잡는다」로 이어진다.
        public static double HitsToFell(IdleState state, IdleTuning tuning)
        {
            return HitsToFellAt(state, tuning, state.Stage);
        }

        /// <summary>그 단계라면 몇 대에 잡히나 — <b>판을 안 건드리고</b> 묻는다.</summary>
        public static double HitsToFellAt(IdleState state, IdleTuning tuning, int stage)
        {
            double damage = DamageOf(state, tuning);
            double durability = TargetHealthAt(stage, tuning);

            if (damage <= 0d || durability <= 0d || double.IsNaN(damage) || double.IsNaN(durability))
            {
                return double.PositiveInfinity;
            }

            double needed = Math.Ceiling(durability / damage - COUNT_EPSILON_RATIO);
            return needed < 1d ? 1d : needed;
        }

        /// <summary>초당 처치 수 — <b>공격 속도를 절대 못 넘는다</b>.</summary>
        public static double KillsPerSecond(IdleState state, IdleTuning tuning)
        {
            double hits = HitsToFell(state, tuning);
            if (double.IsInfinity(hits) || hits <= 0d)
            {
                return 0d;
            }

            return AttackSpeedOf(state, tuning) / hits;
        }
    }
}

