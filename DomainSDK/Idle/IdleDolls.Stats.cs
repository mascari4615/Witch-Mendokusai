using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleDolls.cs 의 Stats 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 인형별 수치와 레벨.
    public static partial class IdleDolls
    {
        /// <summary>
        /// 인형 하나가 <b>들고만 있어도</b> 주는 몫 (비율).
        ///
        /// ★ 이게 수집형의 심장이다 — 파티에 못 들어간 인형도 <b>쓸모가 있어야</b>
        ///   뽑기가 계속 두근거린다. 안 그러면 네 번째부터 나오는 얼굴은 전부 꽝이다.
        /// ★ ★ 을 올리면 이 몫도 같이 오른다 = 중복의 출구가 둘이 된다(승급 자체 + 보유 효과).
        /// </summary>
        public static double OwnedShareOf(IdleDollOwned owned, IdleTuning tuning)
        {
            IdleDollKind kind = KindOf(owned.Id);
            double baseShare = tuning.DollOwnedShareByGrade * GradeWeight(kind.Grade);

            return baseShare * GrowthOf(owned, tuning);
        }

        /// <summary>
        /// ★ 과 레벨이 같이 밀어 올리는 몫 (economy.md 표 3).
        ///
        /// ★ 한 자리에 모음. 두 곳에서 따로 곱하면 한쪽만 고쳤을 때 화면과 판이 갈림
        /// </summary>
        public static double GrowthOf(IdleDollOwned owned, IdleTuning tuning)
        {
            return 1d + owned.Stars * tuning.DollStarStep + owned.Level * tuning.DollLevelStep;
        }

        /// <summary>
        /// 이 인형을 한 레벨 올리는 데 드는 골드.
        ///
        /// ★ 지금 레벨을 따라 오른다. 그래야 골드가 아무리 많아도 레벨은 로그로 눌리고,
        ///   뽑기와 생산자와 강화가 골드를 두고 겨루는 판이 유지됨
        /// </summary>
        public static double LevelCostOf(IdleDollOwned owned, IdleTuning tuning)
        {
            return tuning.DollLevelCostBase * System.Math.Pow(tuning.DollLevelCostRatio, owned.Level);
        }

        /// <summary>
        /// 골드를 내고 한 레벨. 모자라거나 모르는 인형이면 아무 일도 안 일어남
        /// </summary>
        public static bool TryRaiseLevel(IdleState state, IdleTuning tuning, int dollId)
        {
            int at = state.IndexOfDoll(dollId);

            if (at < 0)
            {
                return false;
            }

            IdleDollOwned owned = state.Dolls[at];
            double cost = LevelCostOf(owned, tuning);

            if (state.Resource < cost)
            {
                return false;
            }

            state.Resource -= cost;
            owned.Level += 1;
            state.Dolls[at] = owned;
            return true;
        }

        /// <summary>
        /// 환생이 인형 레벨을 지운다 (U4, decisions-2026-08-30).
        ///
        /// ★ 보유와 ★ 과 도감은 그대로. 골드로 산 것만 사라짐
        /// </summary>
        public static void ForgetLevels(IdleState state)
        {
            for (int index = 0; index < state.Dolls.Count; index++)
            {
                IdleDollOwned owned = state.Dolls[index];
                owned.Level = 0;
                owned.DamageLevel = 0;
                owned.AttackSpeedLevel = 0;
                owned.MaxHealthLevel = 0;
                owned.DefenseLevel = 0;
                owned.CriticalChanceLevel = 0;
                owned.CriticalDamageLevel = 0;
                owned.RecoveryLevel = 0;
                state.Dolls[index] = owned;
            }
        }

        public static double StatValueOf(IdleState state, IdleTuning tuning, int dollId, IdleUpgradeKind kind)
        {
            int index = state.IndexOfDoll(dollId);
            if (index < 0)
            {
                return 0d;
            }

            IdleDollOwned owned = state.Dolls[index];
            return tuning.CurveOf(kind).TotalValueAt(owned.StatLevel(kind));
        }

        public static double CriticalChanceOf(IdleState state, IdleTuning tuning, int dollId)
        {
            double chance = tuning.BaseCriticalChance
                + StatValueOf(state, tuning, dollId, IdleUpgradeKind.CriticalChance);
            return chance > tuning.MaxCriticalChance ? tuning.MaxCriticalChance : chance;
        }

        public static double CriticalDamageOf(IdleState state, IdleTuning tuning, int dollId)
        {
            return tuning.BaseCriticalDamage
                + StatValueOf(state, tuning, dollId, IdleUpgradeKind.CriticalDamage);
        }

        public static double ExpectedCriticalMultiplierOf(IdleState state, IdleTuning tuning, int dollId)
        {
            double chance = CriticalChanceOf(state, tuning, dollId);
            double criticalDamage = CriticalDamageOf(state, tuning, dollId);
            return 1d + chance * (criticalDamage - 1d);
        }

        public static double DefenseOf(IdleState state, IdleTuning tuning, int dollId)
        {
            return StatValueOf(state, tuning, dollId, IdleUpgradeKind.Defense);
        }

        public static double HealPerKillShareOf(IdleState state, IdleTuning tuning, int dollId)
        {
            return tuning.HealPerKillShare
                + StatValueOf(state, tuning, dollId, IdleUpgradeKind.Recovery);
        }

        public static bool TryGetStatCost(IdleState state, IdleTuning tuning, int dollId,
            IdleUpgradeKind kind, int amount, out double cost)
        {
            cost = 0d;
            if (amount != 1 && amount != 10 && amount != 100)
            {
                return false;
            }

            int index = state.IndexOfDoll(dollId);
            if (index < 0)
            {
                return false;
            }

            IdleDollOwned owned = state.Dolls[index];
            int level = owned.StatLevel(kind);
            IUpgradeCurve curve = tuning.CurveOf(kind);

            if (level > curve.MaxLevel - amount)
            {
                return false;
            }

            for (int step = 0; step < amount; step++)
            {
                cost += curve.CostToRaiseFrom(level + step);
            }

            return double.IsNaN(cost) == false && double.IsInfinity(cost) == false;
        }

        public static bool TryRaiseStat(IdleState state, IdleTuning tuning, int dollId,
            IdleUpgradeKind kind, int amount)
        {
            if (TryGetStatCost(state, tuning, dollId, kind, amount, out double cost) == false
                || state.Resource < cost)
            {
                return false;
            }

            int index = state.IndexOfDoll(dollId);
            IdleDollOwned owned = state.Dolls[index];
            owned.SetStatLevel(kind, owned.StatLevel(kind) + amount);
            state.Dolls[index] = owned;
            state.Resource -= cost;
            return true;
        }

        public static bool HasAffordableStat(IdleState state, IdleTuning tuning)
        {
            for (int doll = 0; doll < state.Dolls.Count; doll++)
            {
                int dollId = state.Dolls[doll].Id;
                for (int stat = 0; stat <= (int)IdleUpgradeKind.Recovery; stat++)
                {
                    if (TryGetStatCost(state, tuning, dollId, (IdleUpgradeKind)stat, 1, out double cost)
                        && state.Resource >= cost)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

