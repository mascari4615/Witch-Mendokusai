using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 영웅 <b>도감</b>과 그 셈 (TASK-WM-406).
    ///
    /// ★ 사용자 결정 2026-08-17: 영웅은 <b>가챠로 뽑는 수집형</b>,
    ///   중복은 <b>★ 승급</b>, 보유 효과는 <b>두 겹</b>(개별 보유 + 도감 단계).
    ///
    /// ★ 현재 편성은 <b>메인 세 자리</b> (사용자 결정 2026-09-01):
    ///   <see cref="MAIN_SLOTS"/> 전부 전장에 서서 맞고 때리고 스킬 사용.
    ///   보조 능동 스킬을 따로 기획하기 전까지 <see cref="SUPPORT_SLOTS"/> 는 0.
    ///
    /// ★ 두 겹인 이유 — 한 겹(개별 보유)만 두면 「다 모으면 뭐가 좋은가」가 없어서
    ///   도감을 채울 이유가 사라진다. 세 겹(세트까지)은 영웅 종류가 스무 종을 넘어야
    ///   뜻이 생긴다 — 지금은 빈 약속이 된다.
    ///
    /// ★ <b>같은 갈래끼리 합, 다른 갈래끼리 곱</b> (수집형 키우기 계열 규칙).
    ///   한 갈래에 몰아줄수록 수확이 저절로 체감하고, 새 갈래를 여는 순간이 계단이 된다.
    /// </summary>
    public static partial class IdleHeroes
    {
        /// <summary>
        /// 뽑을 수 있는 영웅들 — 지금은 코드에 둔다.
        ///
        /// ★ 이름은 <b>생김새</b>에서 따온다 (사용자 방향: 세계관 정하기 전이라 기하학 도형).
        ///   컨셉 이름을 지금 박으면 세계관이 정해질 때 전부 거짓말이 된다.
        /// ★ 변의 수가 곧 그림이다 — 화면은 이 수만 알면 그린다.
        /// </summary>
        private static IdleHeroCatalog catalog;

        public static void Configure(IdleHeroCatalog definitions)
        {
            catalog = definitions ?? throw new System.ArgumentNullException(nameof(definitions));
        }

        private static IdleHeroCatalog Catalog => catalog
            ?? throw new System.InvalidOperationException("Idle 영웅 카탈로그가 조립되지 않았다.");

        /// <summary>전장에 서는 자리 수. 이 앞쪽 칸이 <see cref="IdleSquad"/> 의 파티 자리가 된다.</summary>
        public const int MAIN_SLOTS = 3;

        /// <summary>
        /// 전장에 안 서고 보조 스킬만 얹는 자리 수.
        ///
        /// ★ 지금은 0 (사용자 판정 2026-09-01: "영웅도 당장은 3명만, 6명은 추후 기획").
        ///   보조 칸의 값어치는 보조 능동 스킬인데 그게 아직 없어, 칸만 있으면
        ///   <b>배수만 얹는 빈 칸</b> 발생. 코드는 남기고 수만 0
        /// </summary>
        public const int SUPPORT_SLOTS = 0;

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
        public static int Count => Catalog.Count;

        public static IdleHeroKind KindOf(int id)
        {
            return Catalog.KindOf(id);
        }

        /// <summary>이 번호가 <b>이 명단에 있는</b> 얼굴인가 — 저장에서 온 값을 걸러낼 때 쓴다.</summary>
        public static bool Knows(int id)
        {
            return Catalog.Knows(id);
        }

        /// <summary>이 등급의 영웅들 — 뽑기가 등급을 먼저 고르고 그 안에서 하나를 집는다.</summary>
        public static void IdsOfGrade(IdleHeroGrade grade, List<int> into)
        {
            Catalog.IdsOfGrade(grade, into);
        }

        // ── 보유 효과 ①: 개별 보유 ──────────────────────────────────────────

        // ── 보유 효과 ②: 도감 단계 ──────────────────────────────────────────

        // ── 파티 ────────────────────────────────────────────────────────────

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

