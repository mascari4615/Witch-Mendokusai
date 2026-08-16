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

        /// <summary>
        /// 리셋 점수가 주는 배수 — 점수마다 <b>곱한다</b>.
        ///
        /// ★ 처음엔 더했다(점수당 +10%). 이레짜리 시뮬레이션에서 판 소요가 매 판 1.8배씩 늘어
        ///   11판째에 42시간이 됐다 — <b>요구는 지수인데 보상이 선형</b>이었기 때문이다.
        ///   자세한 근거는 <see cref="IdleTuning.PrestigeMultiplierPerPoint"/> 에 적혀 있다.
        /// </summary>
        public static double PrestigeMultiplier(IdleState state, IdleTuning tuning)
        {
            if (state.PrestigePoints <= 0L)
            {
                return 1d;
            }

            return System.Math.Pow(tuning.PrestigeMultiplierPerPoint, state.PrestigePoints);
        }

        /// <summary>
        /// 지금 자리를 비워도 되는 시간(초) — 접을수록 는다.
        /// 접으면 세지는 것 말고 <b>덜 매여도 되는 것</b>도 같이 커진다.
        /// </summary>
        public static double MaxOfflineFor(IdleState state, IdleTuning tuning)
        {
            double allowed = tuning.BaseMaxOfflineSeconds
                + state.Ascensions * tuning.OfflineSecondsPerAscension;

            if (allowed > tuning.MaxOfflineCapSeconds)
            {
                return tuning.MaxOfflineCapSeconds;
            }

            return allowed < 0d ? 0d : allowed;
        }

        /// <summary>가장 좋은 잠재가 주는 배수.</summary>
        public static double PotentialMultiplier(IdleState state)
        {
            return 1d + state.BestPotentialValue;
        }

        /// <summary>지금 한 방의 공격력 — 쌓은 총량 × 리셋 배수 × 잠재 배수.</summary>
        public static double DamageOf(IdleState state, IdleTuning tuning)
        {
            return (tuning.BaseDamage + state.Damage.TotalValue(tuning.DamageCurve))
                * PrestigeMultiplier(state, tuning)
                * PotentialMultiplier(state);
        }

        /// <summary>
        /// 접었을 때 <b>점수가 얼마가 되나</b> — 합계가 아니라 <b>여태 가장 깊이 간 곳</b>이다.
        ///
        /// ★ 처음엔 판마다 더했다. 이레짜리 시뮬레이션이 두 번 다 잡아냈다 (2026-08-16):
        ///   더하고 배수가 선형이면 <b>정체</b>했고(판 소요 1.8배씩 → 11판째 42시간),
        ///   더하고 배수가 지수면 <b>폭주</b>했다(점수가 깊이를, 깊이가 점수를 밀어 3판 만에 77단계).
        ///   되먹임이 문제였다 — 쌓이는 값이 다시 쌓이는 속도를 키웠다.
        ///
        /// ★ 「가장 깊이 간 곳」으로 두면 그 고리가 끊긴다. 점수는 깊이를 <b>따라갈</b> 뿐 못 밀어낸다.
        ///   뜻도 분명해진다 — <b>이미 지나온 길은 다시 안 판다.</b> 접으면 최고 깊이 언저리까지
        ///   단숨에 돌아오고, 거기서부터가 진짜 이번 판이다. 새로 버는 것은 <b>더 내려간 만큼</b>뿐이다.
        /// </summary>
        public static long PrestigeStandingFor(IdleState state, IdleTuning tuning)
        {
            if (state.Stage < tuning.PrestigeMinStage)
            {
                return 0L;
            }

            double standing = (state.Stage - tuning.PrestigeMinStage + 1) * tuning.PrestigePointsPerStage;
            return standing < 0d ? 0L : (long)standing;
        }

        /// <summary>지금 접으면 <b>새로 버는</b> 점수. 이미 가진 것보다 못하면 0 — 접을 이유가 없다.</summary>
        public static long PrestigeAwardFor(IdleState state, IdleTuning tuning)
        {
            long standing = PrestigeStandingFor(state, tuning);
            return standing > state.PrestigePoints ? standing - state.PrestigePoints : 0L;
        }

        /// <summary>지금 접을 수 있나.</summary>
        public static bool CanPrestige(IdleState state, IdleTuning tuning)
        {
            return PrestigeAwardFor(state, tuning) > 0L;
        }

        /// <summary>
        /// 판을 접고 점수로 바꾼다.
        ///
        /// ★ 무엇이 살아남나가 이 게임의 성격을 정한다. <b>점수·가장 깊이·총 처치·본 시각</b>은 남고,
        ///   <b>자원·단계·올린 것</b>은 지워진다. 남는 쪽이 「지난 판이 헛되지 않았다」의 증거이고,
        ///   지워지는 쪽이 「다시 빠르게 내려가는 재미」의 재료다. 둘 중 하나만 있으면 리셋이 벌이 된다.
        /// </summary>
        public static bool TryPrestige(IdleState state, IdleTuning tuning, out long awarded)
        {
            awarded = PrestigeAwardFor(state, tuning);
            if (awarded <= 0L)
            {
                return false;
            }

            state.PrestigePoints = PrestigeStandingFor(state, tuning);
            state.Ascensions += 1;

            state.Resource = 0d;
            state.Stage = 1;
            state.KillsInStage = 0;
            state.HitsOnTarget = 0L;
            state.AttackProgress = 0d;
            state.Damage.Level = 0;
            state.AttackSpeed.Level = 0;
            // 잠재도 남긴다 — 장비가 판을 건너 남는 것과 같은 이치다.
            // 떨어진 것은 남긴다 — 울티마 스쿼드에서 장비가 판을 건너 남는 것과 같다.
            // 그게 「깊이 갔다 온 값어치」의 두 번째 증거다(첫째는 점수).

            return true;
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

        /// <summary>지금 단계의 대상 체력.</summary>
        public static double TargetHealthOf(IdleState state, IdleTuning tuning)
        {
            return tuning.TargetHealthByStage.At(state.Stage - 1);
        }

        /// <summary>지금 단계의 처치 보상.</summary>
        public static double RewardOf(IdleState state, IdleTuning tuning)
        {
            return tuning.RewardByStage.At(state.Stage - 1);
        }

        /// <summary>
        /// 초당 들어오는 자원 — 화면에 「초당 얼마」로 보여줄 값이자 곡선 판정의 축.
        /// <b>지금 단계 기준</b>이다. 내려가면 체력이 보상보다 빨리 올라 이 값이 도로 준다 — 그게 벽이다.
        /// </summary>
        public static double IncomePerSecond(IdleState state, IdleTuning tuning)
        {
            double durability = TargetHealthOf(state, tuning);
            if (durability <= 0d)
            {
                return 0d;
            }

            return KillsPerSecond(state, tuning) * RewardOf(state, tuning);
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
            double damage = DamageOf(state, tuning);
            double durability = TargetHealthOf(state, tuning);

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

        /// <summary>
        /// 시간을 흘린다. 때린 횟수를 세고, 정해진 횟수를 채운 만큼 처치로 넘어간다.
        /// 덜 때운 횟수는 다음으로 이어지므로 <b>스텝을 어떻게 쪼개도 결과가 같다.</b>
        ///
        /// ★ 단계 경계에서 한 번 끊는다 — 단계가 바뀌면 체력도 보상도 바뀐다.
        /// </summary>
        public static void Step(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            double swings = state.AttackProgress + AttackSpeedOf(state, tuning) * seconds;
            long available = (long)(swings + COUNT_EPSILON_RATIO);
            state.AttackProgress = swings - available;

            for (int guard = 0; guard < MAX_STAGES_PER_STEP && available > 0L; guard++)
            {
                double hitsNeeded = HitsToFell(state, tuning);
                if (double.IsInfinity(hitsNeeded))
                {
                    break;
                }

                double reach = (state.HitsOnTarget + available) / hitsNeeded;
                long felled = reach >= long.MaxValue ? long.MaxValue : (long)reach;
                if (felled <= 0L)
                {
                    state.HitsOnTarget += available;
                    available = 0L;
                    break;
                }

                long leftInStage = tuning.KillsPerStage - state.KillsInStage;
                bool clearsStage = tuning.KillsPerStage > 0 && felled >= leftInStage
                    && state.HoldingStage == false;
                long taking = clearsStage ? leftInStage : felled;

                // 큰 수에서도 안 넘치게 double 로 셈하고, 실제로 쓸 수 있는 만큼만 뺀다.
                double wanted = taking * hitsNeeded - state.HitsOnTarget;
                long spent = wanted >= available ? available : (long)wanted;
                state.HitsOnTarget = 0L;
                available -= spent;

                state.Kills += taking;
                state.Resource += taking * RewardOf(state, tuning);
                // ★ 지금 단계에서 잡은 몫이다 — 단계 경계를 넘기 <b>전에</b> 쌓아야
                //   그 처치들이 다음 단계의 높은 상한으로 잘못 쳐지지 않는다.
                IdleDrops.Accrue(state, tuning, taking, state.Stage);

                if (clearsStage == false)
                {
                    // 머무는 동안에는 「이번 단계 처치 수」가 상한에서 멎는다 — 막대가 꽉 찬 채로 계속 잡는다.
                    state.KillsInStage += (int)taking;
                    if (tuning.KillsPerStage > 0 && state.KillsInStage > tuning.KillsPerStage)
                    {
                        state.KillsInStage = tuning.KillsPerStage;
                    }

                    state.HitsOnTarget += available;
                    available = 0L;
                    break;
                }

                state.Stage += 1;
                state.KillsInStage = 0;

                if (state.Stage > state.BestStage)
                {
                    state.BestStage = state.Stage;
                }
            }

            if (available > 0L)
            {
                state.HitsOnTarget += available;
            }
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
