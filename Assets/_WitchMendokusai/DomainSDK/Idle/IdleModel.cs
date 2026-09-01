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
        /// 지금 자리를 비워도 되는 시간(초) — 환생할수록 는다.
        /// 환생하면 세지는 것 말고 <b>덜 매여도 되는 것</b>도 같이 커진다.
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
                * PotentialMultiplier(state)
                * IdleGear.DamageMultiplier(state, tuning)
                // 뽑은 영웅이 <b>실제로</b> 판을 민다 — 안 물리면 뽑기는 도감 놀이다.
                * IdleHeroes.AxisMultiplierOf(state, tuning, IdleHeroAxis.Damage)
                // 도감은 <b>여기서 한 번</b>. 싸움 쪽의 뿌리가 여기다 — 속도·떨구기는
                // 이 값에서 흘러오므로 따로 곱하면 그게 곧 숨은 제곱이 된다.
                * IdleHeroes.CodexMultiplierOf(state, tuning);
        }

        /// <summary>
        /// 환생했을 때 <b>점수가 얼마가 되나</b> — 합계가 아니라 <b>여태 가장 깊이 간 곳</b>이다.
        ///
        /// ★ 처음엔 판마다 더했다. 이레짜리 시뮬레이션이 두 번 다 잡아냈다 (2026-08-16):
        ///   더하고 배수가 선형이면 <b>정체</b>했고(판 소요 1.8배씩 → 11판째 42시간),
        ///   더하고 배수가 지수면 <b>폭주</b>했다(점수가 깊이를, 깊이가 점수를 밀어 3판 만에 77단계).
        ///   되먹임이 문제였다 — 쌓이는 값이 다시 쌓이는 속도를 키웠다.
        ///
        /// ★ 「가장 깊이 간 곳」으로 두면 그 고리가 끊긴다. 점수는 깊이를 <b>따라갈</b> 뿐 못 밀어낸다.
        ///   뜻도 분명해진다 — <b>이미 지나온 길은 다시 안 판다.</b> 환생하면 최고 깊이 언저리까지
        ///   단숨에 돌아오고, 거기서부터가 진짜 이번 판이다. 새로 버는 것은 <b>더 내려간 만큼</b>뿐이다.
        /// </summary>
        public static long PrestigeStandingFor(IdleState state, IdleTuning tuning)
        {
            // ★ <b>가장 깊이 간 곳</b>으로 센다 — 「지금 서 있는 곳」이 아니다.
            //   전에는 state.Stage 를 봤는데, 이 게임은 <b>물러나서 파는 것</b>을 권한다
            //   (TryGoToStage: 「물러나는 데 벌을 주면 아무도 안 물러나고, 그러면 벽에서 게임이 멎는다」).
            //   그래서 500까지 내려갔다가 300으로 물러나 파는 순간 환생 점수가 <b>0</b>이 됐다 —
            //   권장한 행동이 조용히 벌을 받고 있었다. 두 규칙이 서로 반대를 가리키고 있던 것이다.
            int deepest = state.BestStage > state.Stage ? state.BestStage : state.Stage;

            if (deepest < tuning.PrestigeMinStage)
            {
                return 0L;
            }

            double standing = (deepest - tuning.PrestigeMinStage + 1) * tuning.PrestigePointsPerStage;
            return standing < 0d ? 0L : (long)standing;
        }

        /// <summary>지금 환생하면 <b>새로 버는</b> 점수. 이미 가진 것보다 못하면 0 — 환생할 이유가 없다.</summary>
        public static long PrestigeAwardFor(IdleState state, IdleTuning tuning)
        {
            long standing = PrestigeStandingFor(state, tuning);
            return standing > state.PrestigePoints ? standing - state.PrestigePoints : 0L;
        }

        /// <summary>
        /// 환생이 <b>값어치를 갖기 시작하는 깊이</b> — 이미 값어치가 있으면 0.
        ///
        /// ★ 화면이 「더 내려가야 한다」로 끝나면 그건 안내가 아니다. <b>얼마나</b>가 있어야
        ///   사람이 「그럼 거기까지 가 보자」를 정한다. 이 게임이 다른 자리에서 이미 지키는 규칙이다
        ///   (기다림도 「N초 뒤」라고 말한다).
        ///
        /// ★ 셈은 <b>거꾸로</b> 푼다 — 점수는 (깊이 - 최소깊이 + 1) x 단계당점수 이므로,
        ///   지금 점수를 넘기려면 깊이가 얼마여야 하는지 바로 나온다. 한 칸씩 세어 보지 않는다
        ///   (천 단위 깊이에서 그건 못 쓸 셈이다).
        /// </summary>
        public static int PrestigeNextPayingStage(IdleState state, IdleTuning tuning)
        {
            if (PrestigeAwardFor(state, tuning) > 0L)
            {
                return 0;
            }

            if (tuning.PrestigePointsPerStage <= 0d)
            {
                return 0;
            }

            // 점수가 <b>넘어야</b> 하므로 딱 같아지는 깊이의 한 칸 아래까지 구하고 +1.
            double needed = state.PrestigePoints / tuning.PrestigePointsPerStage;
            int stage = tuning.PrestigeMinStage - 1 + (int)System.Math.Floor(needed) + 1;

            if (stage < tuning.PrestigeMinStage)
            {
                stage = tuning.PrestigeMinStage;
            }

            int deepest = state.BestStage > state.Stage ? state.BestStage : state.Stage;

            // 이미 그만큼 갔는데도 안 나온다면 한 칸 더 (반올림이 딱 맞아떨어진 자리).
            while (stage <= deepest)
            {
                stage++;
            }

            return stage;
        }

        /// <summary>지금 환생할 수 있나.</summary>
        public static bool CanPrestige(IdleState state, IdleTuning tuning)
        {
            return PrestigeAwardFor(state, tuning) > 0L;
        }

        /// <summary>
        /// 판을 환생하고 점수로 바꾼다.
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

            // 계산분 + 주운 조각 (economy.md E3 둘 다). 주운 것은 환생에 실려 점수로
            state.PrestigePoints = PrestigeStandingFor(state, tuning) + state.PrestigeShards;
            state.PrestigeShards = 0L;
            // ★ 늘어난 만큼을 <b>쓸 수 있는 돌</b>로도 준다. 배수 쪽(PrestigePoints)은 안 줄어드니
            //   돌을 다 써도 판이 약해지지 않는다 — 그래야 「뽑을까 아낄까」가 함정이 아니라 결정이다.
            state.Stones += awarded;
            state.Ascensions += 1;

            state.Resource = 0d;
            state.Stage = 1;
            state.KillsInStage = 0;
            state.HitsOnTarget = 0L;
            state.AttackProgress = 0d;
            state.Damage.Level = 0;
            state.AttackSpeed.Level = 0;
            // 잠재도 남긴다 — 장비가 판을 건너 남는 것과 같은 이치다.
            // 떨어진 것은 남긴다 — 대열 방치 전투 계열에서 장비가 판을 건너 남는 것과 같다.
            // 그게 「깊이 갔다 온 값어치」의 두 번째 증거다(첫째는 점수).

            return true;
        }

        /// <summary>지금 초당 타격 횟수 — 기본값 + 공격속도 축이 쌓은 총량.</summary>
        public static double AttackSpeedOf(IdleState state, IdleTuning tuning)
        {
            return (tuning.BaseAttackSpeed + state.AttackSpeed.TotalValue(tuning.AttackSpeedCurve))
                * IdleGear.SpeedMultiplier(state, tuning)
                * IdleHeroes.AxisMultiplierOf(state, tuning, IdleHeroAxis.Speed)
                // 폭주는 <b>속도</b>에 건다 — 판이 통째로 빨라지는 게 눈에 가장 잘 보인다.
                * IdleSurge.Multiplier(state, tuning)
                // ★ 쓰러진 자리는 <b>안 때린다</b> (V2 부대층). 전원 서 있으면 1 이라
                //   이 층을 얹기 전의 곡선·시험이 그대로 산다.
                * IdleSquad.FightingShare(state);
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

            // 전장에 하나 있어야 판이 돎. 자리 0(나) 삭제 뒤로는 시작 인형이 그 몫 (C10)
            IdleHeroes.EnsureStarter(state);

            // ★ 보급(카드)이 스텝 <b>중간에</b> 끝나면 경계에서 한 번 끊는다 — 수입 배수가
            //   스텝 안에서 상수여야 「60초 한 번 == 0.1초 600번」이 선다.
            if (state.SupplySecondsLeft > 0d && seconds > state.SupplySecondsLeft)
            {
                double boosted = state.SupplySecondsLeft;
                StepFlat(state, tuning, boosted);
                StepFlat(state, tuning, seconds - boosted);
                return;
            }

            StepFlat(state, tuning, seconds);
        }

        /// <summary>
        /// <b>보고 있는 동안</b>의 한 스텝 — 적이 때리고, 쓰러지고, 일어난다 (V2 부대층).
        ///
        /// ★ 왜 <see cref="Step"/> 과 갈랐나 — <b>자는 동안 전멸</b>은 방치형에서 최악이다.
        ///   8시간을 비웠는데 첫 20분에 전멸해 나머지가 통째로 헛돈다면, 그건 도전이 아니라 벌이다.
        ///   그래서 위험은 <b>화면 앞에 있을 때</b>만 흐른다 — 폭주(<see cref="IdleSurge"/>)를
        ///   자리 비운 동안 지우는 것과 같은 이치다.
        ///
        /// ★ 자리 비운 몫(<see cref="IdleSession.CatchUp"/>)과 곡선 시뮬은 <see cref="Step"/> 을 쓴다.
        ///   그래서 오프라인 정산은 여전히 <b>결정적·스텝 불변</b>이다.
        /// </summary>
        public static void StepLive(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            IdleHeroes.EnsureStarter(state);
            state.EnsureSeatRoom(tuning);

            // 골드, 코스트, 보급은 수식 그대로. 보급 만료가 중간이면 경계에서 분할
            if (state.SupplySecondsLeft > 0d && seconds > state.SupplySecondsLeft)
            {
                double boosted = state.SupplySecondsLeft;
                StepEconomy(state, tuning, boosted);
                StepEconomy(state, tuning, seconds - boosted);
            }
            else
            {
                StepEconomy(state, tuning, seconds);
            }

            // 싸움은 시뮬 (combat.md). 사거리, 이동, 타격, 처치, 전멸, 부활 전부 그 안
            IdleBattleSim.Advance(state, tuning, seconds);
        }

        /// <summary>
        /// 자리 비운 몫 (combat.md 6). 실측이 있으면 초당 처치 x 시간 x 오프라인 몫.
        /// 구역 진행 없음 (자는 동안 전멸도 전진도 없음). 실측이 없으면 옛 수식 <see cref="Step"/>
        /// </summary>
        public static void StepAway(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            if (state.MeasuredKillsPerSecond <= 0d || state.MeasuredStage <= 0)
            {
                Step(state, tuning, seconds);
                return;
            }

            IdleHeroes.EnsureStarter(state);

            if (state.SupplySecondsLeft > 0d && seconds > state.SupplySecondsLeft)
            {
                double boosted = state.SupplySecondsLeft;
                StepEconomy(state, tuning, boosted);
                StepEconomy(state, tuning, seconds - boosted);
            }
            else
            {
                StepEconomy(state, tuning, seconds);
            }

            long kills = (long)(state.MeasuredKillsPerSecond * seconds * tuning.OfflineKillShare + COUNT_EPSILON_RATIO);
            if (kills <= 0L)
            {
                return;
            }

            state.Kills += kills;
            IdleDrops.Accrue(state, tuning, kills, state.Stage);
            RollStoneDrop(state, tuning, kills);
        }

        /// <summary>
        /// 한 스텝에서 끊을 수 있는 사건 수의 상한 — <b>멈추지 않는 판</b>을 막는 안전선.
        ///
        /// ★ 넉넉해야 한다 (실측 2026-08-23): 512 로 뒀더니 <b>자리 비운 8시간</b>을 한 번에 밟을 때
        ///   부활(12초마다)이 2400번이라 <b>시간이 남은 채 잘렸다</b> — 그러면 쪼개 밟은 판과 갈린다.
        ///
        /// ★ 그렇다고 무한도 안 된다 — 이레짜리 시뮬을 한 번에 밟으면 조각이 수만 개라
        ///   에디터가 멎는다. 실제 게임의 한 번(오프라인 상한 24시간)은 여기 안 닿는다.
        ///   넘치면 남은 시간을 <b>통째로</b> 밟는다(사건 경계를 못 지키므로 그때만 근사).
        /// </summary>
        private const int MAX_EVENT_SLICES = 8192;

        private static void StepFlat(IdleState state, IdleTuning tuning, double seconds)
        {
            StepEconomy(state, tuning, seconds);
            state.AttackProgress += AttackSpeedOf(state, tuning) * seconds;
            Resolve(state, tuning);
        }

        /// <summary>싸움을 뺀 시간의 몫. 코스트, 기지 산출, 보급 만료</summary>
        private static void StepEconomy(IdleState state, IdleTuning tuning, double seconds)
        {
            // 코스트는 시간이 채운다 — 상한에서 멎는다 (V2 카드층).
            state.Cost += tuning.CostPerSecond * seconds;
            if (state.Cost > tuning.CostMax)
            {
                state.Cost = tuning.CostMax;
            }

            // 기지가 시간만큼 자원을 낸다 — 잡든 안 잡든 돈다.
            state.Resource += IdleBase.OutputPerSecond(state, tuning) * seconds;

            if (state.SupplySecondsLeft > 0d)
            {
                state.SupplySecondsLeft -= seconds;
                if (state.SupplySecondsLeft < 1e-12d)
                {
                    state.SupplySecondsLeft = 0d;
                }
            }
        }

        /// <summary>
        /// 자동 공격 <paramref name="seconds"/>초치를 <b>즉시</b> 몰아친다 — 손 때리기와
        /// 일제 사격 카드가 같은 길을 탄다 (두 벌이면 언젠가 갈린다).
        ///
        /// ★ 사람이 부르는 것이라 스텝 불변의 대상이 아니다 — 시간은 안 흐른다.
        /// </summary>
        public static void StrikeFor(IdleState state, IdleTuning tuning, double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            // 라이브 전장이 있으면 시뮬 위에서 (combat.md 5)
            if (state.Battle.Ready)
            {
                IdleBattleSim.StrikeFor(state, tuning, seconds);
                return;
            }

            state.AttackProgress += AttackSpeedOf(state, tuning) * seconds;
            Resolve(state, tuning);
        }

        /// <summary>
        /// <b>손으로 한 대</b> — 사람이 판을 눌렀다 (TASK-WM-406).
        ///
        /// ★ 왜 있나 (사용자 지적: 「전혀 클리커스럽지 않다」) — 이 판은 전부 자동이라
        ///   <b>누를 것이 없었다</b>. 생산자 클리커 계열의 심장은 큰 버튼이고, 방치형이 방치로만
        ///   이루어지면 시작한 첫 1분이 <b>구경</b>이 된다.
        ///
        /// ★ 한 대의 값을 <b>지금 공격속도의 몇 초치</b>로 준다 — 고정값으로 주면
        ///   초반엔 과하고 후반엔 아무것도 아니게 된다. 비율로 주면 손은 <b>늘 같은 몫</b>을 하고,
        ///   그래서 「눌러도 그만」이 안 된다. 안 눌러도 손해는 없다(방치형이니까).
        ///
        /// ★ 이건 사람이 부르는 것이라 <b>스텝 불변</b>의 대상이 아니다 — 감정(도박)과 같은 갈래다.
        /// </summary>
        public static void Tap(IdleState state, IdleTuning tuning)
        {
            StrikeFor(state, tuning, tuning.TapSecondsOfAttack * IdleSurge.HandMultiplier(state, tuning));
        }

        /// <summary>
        /// 쌓인 공격을 <b>실제 처치로</b> 바꾼다 — 시간이 쌓았든 손이 쌓았든 같은 길을 탄다.
        ///
        /// ★ 한 길로 모아 둔다: 손으로 때리기가 다른 셈을 쓰면 그건 두 게임이 된다.
        /// </summary>
        private static void Resolve(IdleState state, IdleTuning tuning)
        {
            long available = (long)(state.AttackProgress + COUNT_EPSILON_RATIO);
            if (available <= 0L)
            {
                return;
            }

            state.AttackProgress -= available;

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
                // ★ 반복 중이면 <b>안 내려간다</b> (V2 방향 6) — 실패한 판에 자동으로 다시
                //   밀어 넣지 않는다. 다시 갈지는 사람이 「다음 구역」으로 정한다.
                bool clearsStage = tuning.KillsPerStage > 0 && felled >= leftInStage
                    && state.HoldingStage == false && state.Repeating == false;
                long taking = clearsStage ? leftInStage : felled;

                // 큰 수에서도 안 넘치게 double 로 셈하고, 실제로 쓸 수 있는 만큼만 뺀다.
                double wanted = taking * hitsNeeded - state.HitsOnTarget;
                long spent = wanted >= available ? available : (long)wanted;
                state.HitsOnTarget = 0L;
                available -= spent;

                state.Kills += taking;
                // 잡은 만큼 숨을 돌린다 — 잡힌 적은 더 이상 안 때린다 (V2 부대층).
                IdleSquad.HealOnKills(state, tuning, taking);
                // ★ 잡기는 <b>자원을 안 낸다</b> — 자원은 기지가 낸다(사용자 방향: 클리커 + 모험).
                //   갈라 놓아야 두 층이 서로를 부른다. 합쳐 두면 기지가 있을 이유가 없다.
                // ★ 지금 단계에서 잡은 몫이다 — 단계 경계를 넘기 <b>전에</b> 쌓아야
                //   그 처치들이 다음 단계의 높은 상한으로 잘못 쳐지지 않는다.
                IdleDrops.Accrue(state, tuning, taking, state.Stage);
                RollStoneDrop(state, tuning, taking);

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

                // 방금 이 구역을 깼다 — 실패하면 여기까지 물러난다 (V2 방향 6).
                //   마지막 하나는 보스라 환생 조각이 떨어진다 (economy.md 표 2)
                DropPrestigeShard(state, tuning);
                state.ClearedStage = state.Stage;

                // ★ 구역을 깨면 <b>재정비</b>한다 — 회복이 없으면 시간이 지나는 것만으로 반드시 죽는다.
                //   그러면 벽이 「내 세기」가 아니라 「시계」가 되어 성장이 뜻을 잃는다.
                //   웨이브를 다 밀었으니 숨을 돌린다 — 자동전투+카드 개입 계열·대열 방치 전투 계열의 구역 사이 그 자리다.
                IdleSquad.HealAll(state, tuning);

                state.Stage += 1;
                state.KillsInStage = 0;

                RewardNewDepth(state, tuning);
            }

            if (available > 0L)
            {
                state.HitsOnTarget += available;
            }
        }

        /// <summary>
        /// 보스를 잡아 환생 조각을 줍는다 (economy.md 표 2, E3)
        ///
        /// ★ 계산분과 그릇이 다름. 그래야 환생이 대입할 때 주운 것이 안 사라짐
        /// </summary>
        public static void DropPrestigeShard(IdleState state, IdleTuning tuning)
        {
            if (tuning.ShardsPerBoss > 0L)
            {
                state.PrestigeShards += tuning.ShardsPerBoss;
            }
        }

        /// <summary>
        /// 새 깊이에 닿았으면 최고 기록을 올리고 뽑기 재화를 준다 (economy.md 표 2)
        ///
        /// ★ 라이브(<c>IdleBattleSim</c>)와 오프라인(<c>Resolve</c>) 공용
        ///   두 벌이면 어느 한쪽에서만 재화가 나옴. 자는 동안 손해의 새 얼굴
        /// </summary>
        public static void RewardNewDepth(IdleState state, IdleTuning tuning)
        {
            if (state.Stage <= state.BestStage)
            {
                return;
            }

            state.BestStage = state.Stage;

            if (tuning.StonesPerFirstClear > 0L)
            {
                state.Stones += tuning.StonesPerFirstClear;
            }
        }

        /// <summary>
        /// 처치가 뽑기 재화를 떨구나 (economy.md 표 2, 낮은 확률)
        ///
        /// ★ 판이 든 주사위. 저장에 실리므로 껐다 켜서 다시 굴리기 불가
        /// </summary>
        public static void RollStoneDrop(IdleState state, IdleTuning tuning, long kills)
        {
            if (kills <= 0L || tuning.StoneDropChance <= 0d)
            {
                return;
            }

            IdleRandom dice = new IdleRandom(state.RandomState);

            for (long at = 0; at < kills; at++)
            {
                if (dice.NextDouble() < tuning.StoneDropChance)
                {
                    state.Stones += 1L;
                }
            }

            state.RandomState = dice.State;
        }

        /// <summary>
        /// 한 방에 잡히는 <b>가장 깊은</b> 자리 — 거기가 가장 잘 벌린다.
        ///
        /// ★ 규칙이라 코어가 안다. 화면이 「어디로 물러날까」를 스스로 계산하면
        ///   창마다 다른 답을 내고, 그건 같은 판이 다르게 보이는 것이다.
        ///
        /// ★ 이 자리가 왜 최선인가 — 한 방에 잡히면 처치 속도가 <b>공격 속도 그대로</b>다.
        ///   더 깊이 가면 여러 번 때려야 해 느려지고, 더 얕으면 보상이 작다.
        ///   이분 탐색이다(깊이가 천 단위여도 열 몇 번이면 찾는다).
        /// </summary>
        public static int BestFarmingStage(IdleState state, IdleTuning tuning)
        {
            // ⚠ 전에는 <b>판의 단계를 바꿔 가며</b> 찾고 마지막에 되돌렸다. 이 자리는 화면이
            //   <b>매 프레임</b> 부르는 곳이라, 그 사이에 무슨 일이 나면 사람이 엉뚱한 깊이에
            //   서 있게 된다. 「사면 몇 배」에서 고친 것과 같은 병이라 같이 고친다 —
            //   <b>묻기만 하는 자리는 판을 안 건드린다.</b>
            int low = 1;
            int high = state.BestStage < 1 ? 1 : state.BestStage;

            while (low < high)
            {
                int mid = low + (high - low + 1) / 2;

                if (HitsToFellAt(state, tuning, mid) <= 1d)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        /// <summary>
        /// 그 자리로 옮길 수 있나 — 화면이 <b>버튼을 켤지</b> 정하는 답.
        ///
        /// ★ 이 판정이 화면에도 한 벌 있으면 언젠가 갈린다. 버튼은 켜져 있는데 눌러도
        ///   아무 일이 안 나는 상태가 그렇게 생긴다 — 오늘 감정·합치기에서 같은 꼴을 두 번 고쳤다.
        /// </summary>
        public static bool CanGoToStage(IdleState state, int stage)
        {
            return stage >= 1 && stage <= state.BestStage && stage != state.Stage;
        }

        /// <summary>
        /// 이미 지나온 자리로 옮긴다 — <b>앞질러 갈 수는 없다</b>.
        ///
        /// ★ 옮기면 이번 대상 진행은 버린다(다른 대상이니까). 그 외에는 아무것도 안 잃는다 —
        ///   물러나는 데 벌을 주면 아무도 안 물러나고, 그러면 벽에서 게임이 멎는다.
        /// </summary>
        public static bool TryGoToStage(IdleState state, int stage)
        {
            if (CanGoToStage(state, stage) == false)
            {
                return false;
            }

            state.Stage = stage;
            state.KillsInStage = 0;
            state.HitsOnTarget = 0L;
            state.AttackProgress = 0d;
            return true;
        }

        /// <summary>
        /// 살 수 있는 만큼 <b>싼 축부터</b> 올린다 — 몇 번 올렸는지 돌려준다 (TASK-WM-406).
        ///
        /// ★ 생산자에만 몰아 사기를 두면 절반짜리다 — 강화도 중반부터 같은 노동이 된다.
        ///   판단(무엇을 올릴까)은 그대로 두고 손가락 일만 덜어낸다.
        /// ★ 싼 축부터 = 같은 자원으로 가장 많이 올리는 순서. 시험(IdlePlay)이 쓰던 규칙 그대로다 —
        ///   규칙이 시험에만 있고 게임에는 없으면, 사람은 시험보다 못한 판을 논다.
        /// </summary>
        /// <summary>
        /// 지금 <b>올릴 수 있는 축 중 싼 쪽</b> — 하나도 없으면 false.
        ///
        /// ★ 몰아 올리기의 한 걸음이자, 화면이 <b>버튼을 켤지</b> 정하는 답이다.
        ///   규칙이 두 벌이면 버튼은 켜져 있고 눌러도 아무 일이 안 나는 상태가 생긴다.
        /// </summary>
        public static bool CheapestRaisableAxis(IdleState state, IdleTuning tuning, out IdleUpgradeKind pick)
        {
            bool hasDamage = TryGetNextCost(state, tuning, IdleUpgradeKind.Damage, out double damageCost);
            bool hasSpeed = TryGetNextCost(state, tuning, IdleUpgradeKind.AttackSpeed, out double speedCost);

            bool canDamage = hasDamage && damageCost <= state.Resource;
            bool canSpeed = hasSpeed && speedCost <= state.Resource;

            if (canDamage == false && canSpeed == false)
            {
                pick = IdleUpgradeKind.Damage;
                return false;
            }

            pick = canDamage && (canSpeed == false || damageCost <= speedCost)
                ? IdleUpgradeKind.Damage
                : IdleUpgradeKind.AttackSpeed;

            return true;
        }

        public static int RaiseAsManyAsAfforded(IdleState state, IdleTuning tuning, int most)
        {
            int raised = 0;

            while (raised < most)
            {
                if (CheapestRaisableAxis(state, tuning, out IdleUpgradeKind pick) == false)
                {
                    break;
                }

                if (TryRaise(state, tuning, pick, out UpgradeRaiseFailure _) == false)
                {
                    break;
                }

                raised++;
            }

            return raised;
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
