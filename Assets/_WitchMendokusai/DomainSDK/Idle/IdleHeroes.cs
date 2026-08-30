using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 영웅 <b>도감</b>과 그 셈 (TASK-WM-406).
    ///
    /// ★ 사용자 결정 2026-08-17: 영웅은 <b>가챠로 뽑는 수집형</b>,
    ///   중복은 <b>★ 승급</b>, 보유 효과는 <b>두 겹</b>(개별 보유 + 도감 단계).
    ///
    /// ★ 편성은 <b>여섯 자리</b> (사용자 결정 2026-08-30, 자동전투+카드 개입 계열 문법 그대로):
    ///   앞 <see cref="MAIN_SLOTS"/> 는 <b>메인</b>: 전장에 서서 맞고 때리고 스킬 사용.
    ///   뒤 <see cref="SUPPORT_SLOTS"/> 는 <b>보조</b>: 전장 불참(자리도 체력도 없음).
    ///   보조 스킬만 얹는다. 지금은 그 몫이 축 배수(메인보다 작게)로 들어가고,
    ///   능동 보조 스킬(카드층)은 다음 조각이다. 정본 <c>memo/wm/design/idle/decisions-2026-08-30.md</c>.
    ///
    /// ★ 두 겹인 이유 — 한 겹(개별 보유)만 두면 「다 모으면 뭐가 좋은가」가 없어서
    ///   도감을 채울 이유가 사라진다. 세 겹(세트까지)은 영웅 종류가 스무 종을 넘어야
    ///   뜻이 생긴다 — 지금은 빈 약속이 된다.
    ///
    /// ★ <b>같은 갈래끼리 합, 다른 갈래끼리 곱</b> (수집형 키우기 계열 규칙).
    ///   한 갈래에 몰아줄수록 수확이 저절로 체감하고, 새 갈래를 여는 순간이 계단이 된다.
    /// </summary>
    public static class IdleHeroes
    {
        /// <summary>
        /// 뽑을 수 있는 영웅들 — 지금은 코드에 둔다.
        ///
        /// ★ 이름은 <b>생김새</b>에서 따온다 (사용자 방향: 세계관 정하기 전이라 기하학 도형).
        ///   컨셉 이름을 지금 박으면 세계관이 정해질 때 전부 거짓말이 된다.
        /// ★ 변의 수가 곧 그림이다 — 화면은 이 수만 알면 그린다.
        /// </summary>
        private static readonly IdleHeroKind[] ALL =
        {
            new IdleHeroKind(0, "세모", IdleHeroAxis.Damage, IdleHeroGrade.Common, 3),
            new IdleHeroKind(1, "네모", IdleHeroAxis.Base, IdleHeroGrade.Common, 4),
            new IdleHeroKind(2, "다섯모", IdleHeroAxis.Drop, IdleHeroGrade.Common, 5),
            new IdleHeroKind(3, "여섯모", IdleHeroAxis.Speed, IdleHeroGrade.Common, 6),

            new IdleHeroKind(4, "쐐기", IdleHeroAxis.Damage, IdleHeroGrade.Rare, 3),
            new IdleHeroKind(5, "벽돌", IdleHeroAxis.Base, IdleHeroGrade.Rare, 4),
            new IdleHeroKind(6, "별모", IdleHeroAxis.Drop, IdleHeroGrade.Rare, 5),
            new IdleHeroKind(7, "톱니", IdleHeroAxis.Speed, IdleHeroGrade.Rare, 7),

            new IdleHeroKind(8, "칼날", IdleHeroAxis.Damage, IdleHeroGrade.Epic, 3),
            new IdleHeroKind(9, "성채", IdleHeroAxis.Base, IdleHeroGrade.Epic, 6),
            new IdleHeroKind(10, "그물", IdleHeroAxis.Drop, IdleHeroGrade.Epic, 8),
            new IdleHeroKind(11, "회오리", IdleHeroAxis.Speed, IdleHeroGrade.Epic, 9),

            new IdleHeroKind(12, "송곳", IdleHeroAxis.Damage, IdleHeroGrade.Legend, 3),
            new IdleHeroKind(13, "고리", IdleHeroAxis.Base, IdleHeroGrade.Legend, 10),
            new IdleHeroKind(14, "여울", IdleHeroAxis.Drop, IdleHeroGrade.Legend, 11),
            new IdleHeroKind(15, "번개", IdleHeroAxis.Speed, IdleHeroGrade.Legend, 12),
        };

        /// <summary>전장에 서는 자리 수. 이 앞쪽 칸이 <see cref="IdleSquad"/> 의 파티 자리가 된다.</summary>
        public const int MAIN_SLOTS = 3;

        /// <summary>전장에 안 서고 보조 스킬만 얹는 자리 수.</summary>
        public const int SUPPORT_SLOTS = 3;

        /// <summary>편성 칸 수. 메인 + 보조.</summary>
        public const int PARTY_SLOTS = MAIN_SLOTS + SUPPORT_SLOTS;

        /// <summary>
        /// 시작 인형. 플레이어 인형(자리 0, 늘 있던 나)을 뺀 자리의 대체
        /// (사용자 결정 C10, 2026-08-30. 정본 <c>memo/wm/design/idle/decisions-2026-08-30.md</c>)
        ///
        /// ★ 뽑기 전에도 전장에 하나 필수. 아무도 없으면 처치 0 → 골드 0 → 뽑기 재화 0. 첫 인형은 게임 지급
        /// </summary>
        public const int STARTER_ID = 0;

        /// <summary>
        /// 시작 인형 보장. 인형 0명이면 <see cref="STARTER_ID"/> 지급,
        /// 메인 칸 전부 비면 첫 메인 칸에 착석. 바뀐 것이 있으면 참
        ///
        /// ★ 새 판, 옛 저장(자리 0 시절), 환생 뒤 어디서 와도 전장에 최소 하나
        /// ★ 사람이 메인 칸을 다 비워도 하나는 착석. 빈 전장은 놀 수 없는 판
        /// </summary>
        public static bool EnsureStarter(IdleState state)
        {
            bool changed = false;

            if (state.Heroes.Count == 0)
            {
                state.Heroes.Add(new IdleHeroOwned(STARTER_ID));
                changed = true;
            }

            for (int slot = 0; slot < MAIN_SLOTS; slot++)
            {
                if (state.Party[slot] >= 0 && state.IndexOfHero(state.Party[slot]) >= 0)
                {
                    return changed;
                }
            }

            int first = state.Heroes[0].Id;
            for (int slot = 0; slot < state.Party.Length; slot++)
            {
                if (state.Party[slot] == first)
                {
                    state.Party[slot] = -1;
                }
            }

            state.Party[0] = first;
            return true;
        }

        /// <summary>이 칸이 메인(전장에 서는) 칸인가.</summary>
        public static bool IsMainSlot(int slot)
        {
            return slot >= 0 && slot < MAIN_SLOTS;
        }

        /// <summary>빈 편성. 모든 칸이 -1.</summary>
        public static int[] EmptyParty()
        {
            int[] party = new int[PARTY_SLOTS];

            for (int slot = 0; slot < party.Length; slot++)
            {
                party[slot] = -1;
            }

            return party;
        }

        /// <summary>뽑을 수 있는 영웅 수.</summary>
        public static int Count => ALL.Length;

        public static IdleHeroKind KindOf(int id)
        {
            return ALL[id];
        }

        /// <summary>이 번호가 <b>이 명단에 있는</b> 얼굴인가 — 저장에서 온 값을 걸러낼 때 쓴다.</summary>
        public static bool Knows(int id)
        {
            return id >= 0 && id < ALL.Length;
        }

        /// <summary>이 등급의 영웅들 — 뽑기가 등급을 먼저 고르고 그 안에서 하나를 집는다.</summary>
        public static void IdsOfGrade(IdleHeroGrade grade, List<int> into)
        {
            into.Clear();

            for (int index = 0; index < ALL.Length; index++)
            {
                if (ALL[index].Grade == grade)
                {
                    into.Add(ALL[index].Id);
                }
            }
        }

        // ── 보유 효과 ①: 개별 보유 ──────────────────────────────────────────

        /// <summary>
        /// 영웅 하나가 <b>들고만 있어도</b> 주는 몫 (비율).
        ///
        /// ★ 이게 수집형의 심장이다 — 파티에 못 들어간 영웅도 <b>쓸모가 있어야</b>
        ///   뽑기가 계속 두근거린다. 안 그러면 네 번째부터 나오는 얼굴은 전부 꽝이다.
        /// ★ ★ 을 올리면 이 몫도 같이 오른다 = 중복의 출구가 둘이 된다(승급 자체 + 보유 효과).
        /// </summary>
        public static double OwnedShareOf(IdleHeroOwned owned, IdleTuning tuning)
        {
            IdleHeroKind kind = KindOf(owned.Id);
            double baseShare = tuning.HeroOwnedShareByGrade * GradeWeight(kind.Grade);

            return baseShare * (1d + owned.Stars * tuning.HeroStarStep);
        }

        /// <summary>
        /// 한 축의 <b>보유 배수</b> — 같은 갈래는 더하고, 축끼리는 부르는 쪽이 따로 곱한다.
        /// </summary>
        public static double OwnedMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            double sum = 0d;

            for (int index = 0; index < state.Heroes.Count; index++)
            {
                IdleHeroOwned owned = state.Heroes[index];
                if (KindOf(owned.Id).Axis != axis)
                {
                    continue;
                }

                sum += OwnedShareOf(owned, tuning);
            }

            return 1d + sum;
        }

        // ── 보유 효과 ②: 도감 단계 ──────────────────────────────────────────

        /// <summary>
        /// 도감 점수 — <b>모은 종류 + 올린 ★</b>. 「많이 모을수록」과 「깊이 키울수록」을 한 수로 묶는다.
        /// </summary>
        public static int CodexScoreOf(IdleState state)
        {
            int score = 0;

            for (int index = 0; index < state.Heroes.Count; index++)
            {
                score += 1 + state.Heroes[index].Stars;
            }

            return score;
        }

        /// <summary>
        /// 도감이 주는 <b>전체 배수</b> — 축과 무관하게 판 전체에 곱한다.
        ///
        /// ★ 문턱마다 한 계단씩 오른다. 매끈하게 오르면 「채운 순간」이 안 느껴진다 —
        ///   느껴져야 채울 이유가 된다.
        /// </summary>
        public static double CodexMultiplierOf(IdleState state, IdleTuning tuning)
        {
            if (tuning.CodexStepScore <= 0)
            {
                return 1d;
            }

            int steps = CodexScoreOf(state) / tuning.CodexStepScore;
            return 1d + steps * tuning.CodexStepBonus;
        }

        // ── 파티 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 편성한 얼굴이 주는 몫. <b>편성한 영웅만</b> 이 배수를 준다(보유 효과와 별개).
        ///
        /// ★ 보유는 가지고만 있어도, 편성은 내보내야. 둘을 갈라야
        ///   <b>누구를 내보낼까</b>가 결정이 된다 — 안 그러면 전원 참전이 늘 정답이다.
        /// ★ 메인 칸과 보조 칸의 몫이 다르다. 보조는 전장에 안 서서 맞지도 않으니
        ///   같은 몫이면 늘 보조가 정답이 된다. 보조 몫은 <see cref="IdleTuning.HeroSupportShareByGrade"/>.
        /// </summary>
        public static double PartyMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            double sum = 0d;

            for (int slot = 0; slot < state.Party.Length; slot++)
            {
                int id = state.Party[slot];
                if (id < 0)
                {
                    continue;
                }

                int at = state.IndexOfHero(id);
                if (at < 0)
                {
                    continue;
                }

                IdleHeroOwned owned = state.Heroes[at];
                if (KindOf(id).Axis != axis)
                {
                    continue;
                }

                double share = IsMainSlot(slot)
                    ? tuning.HeroPartyShareByGrade
                    : tuning.HeroSupportShareByGrade;

                sum += share * GradeWeight(KindOf(id).Grade)
                    * (1d + owned.Stars * tuning.HeroStarStep);
            }

            return 1d + sum;
        }

        /// <summary>
        /// 한 축이 지금 받는 배수 — <b>보유 × 파티</b>.
        ///
        /// ⚠ 도감은 여기 <b>안 들어간다</b>. 도감은 「축과 무관하게 판 전체에 한 번」인데
        ///   여기 넣어 두니 축마다 한 번씩, 즉 <b>네 군데</b>에서 곱해지고 있었다.
        ///   처치 속도는 공격력 × 공격속도라 도감이 <b>제곱</b>으로 들어갔고
        ///   (「판 전체 x1.10」이 실제로는 x1.21), 떨구기는 그 위에 또 한 겹이었다.
        ///   숨은 지수는 곡선을 통째로 거짓말로 만든다 — 그래서 <b>뿌리 둘</b>에서만 곱한다
        ///   (<see cref="IdleModel.DamageOf"/> · <see cref="IdleBase.OutputPerSecond"/>).
        ///   나머지는 그 둘에서 흘러오므로 저절로 정확히 한 번 받는다.
        /// </summary>
        public static double AxisMultiplierOf(IdleState state, IdleTuning tuning, IdleHeroAxis axis)
        {
            return OwnedMultiplierOf(state, tuning, axis)
                * PartyMultiplierOf(state, tuning, axis);
        }

        /// <summary>등급이 몫에 곱해지는 무게 — 위 등급일수록 크게.</summary>
        public static double GradeWeight(IdleHeroGrade grade)
        {
            switch (grade)
            {
                case IdleHeroGrade.Rare: return 2d;
                case IdleHeroGrade.Epic: return 5d;
                case IdleHeroGrade.Legend: return 12d;
                default: return 1d;
            }
        }

        public static string NameOfGrade(IdleHeroGrade grade)
        {
            switch (grade)
            {
                case IdleHeroGrade.Rare: return "레어";
                case IdleHeroGrade.Epic: return "에픽";
                case IdleHeroGrade.Legend: return "레전드";
                default: return "일반";
            }
        }

        public static string NameOfAxis(IdleHeroAxis axis)
        {
            switch (axis)
            {
                case IdleHeroAxis.Speed: return "속도";
                case IdleHeroAxis.Base: return "기지";
                case IdleHeroAxis.Drop: return "떨구기";
                default: return "공격";
            }
        }
    }
}
