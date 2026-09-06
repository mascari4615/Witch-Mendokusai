using System;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleModel.cs 의 Step 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 시간을 흘리는 쪽.
    public static partial class IdleModel
    {
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
    }
}

