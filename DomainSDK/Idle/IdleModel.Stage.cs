using System;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleModel.cs 의 Stage 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 구역 이동과 드롭.
    public static partial class IdleModel
    {
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

        /// <summary>한 영웅의 한 수치를 정해진 클릭 묶음만큼 올림</summary>
        public static bool TryRaise(IdleState state, IdleTuning tuning, int heroId,
            IdleUpgradeKind kind, int amount)
        {
            return IdleHeroes.TryRaiseStat(state, tuning, heroId, kind, amount);
        }

        public static bool TryGetCost(IdleState state, IdleTuning tuning, int heroId,
            IdleUpgradeKind kind, int amount, out double cost)
        {
            return IdleHeroes.TryGetStatCost(state, tuning, heroId, kind, amount, out cost);
        }
    }
}

