using System;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleModel.cs 의 Prestige 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 환생 셈.
    public static partial class IdleModel
    {
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
            // 인형 레벨도 지움 (U4). 보유와 ★ 과 도감은 그대로
            IdleHeroes.ForgetLevels(state);

            // 상점에서 산 것도 지움 (사용자 판정 2026-09-01. 골드로 산 것은 그 판의 것)
            IdleShop.ForgetPurchases(state);

            // 장비도 지움 (economy.md 표 3, 사용자 판정 2026-09-01 "장비도 리셋").
            //
            // ★ 남기던 때는 장비가 판을 건너는 것이 <b>깊이 갔다 온 값어치</b>라고 봤는데,
            //   그러면 환생이 되감기가 아니라 <b>덧칠</b>이 된다. 매 판 다시 모으는 것이
            //   있어야 새 판이 새 판이 됨
            //
            // ★ 감정 개수(<c>DroppedByTier</c>)와 잠재 기록은 남긴다. 그건 장비가 아니라
            //   <b>판을 얼마나 깊이 갔나</b> 의 기록. 지우면 감정 카드가 매 판 잠김
            state.Bag.Clear();

            for (int index = 0; index < state.Worn.Length; index++)
            {
                state.Worn[index] = default;
            }

            return true;
        }
    }
}

