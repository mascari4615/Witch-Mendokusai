using System;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 영웅이 밀어 주는 <b>축</b> (TASK-WM-406).
    ///
    /// ★ 갈래를 나눠 두는 이유는 <b>보유 효과의 곱셈 규칙</b> 때문이다 (사용자 컨펌 2026-08-17,
    ///   근거 = <c>refs/korean-idle-gacha.md</c> 수집형 키우기 계열).
    ///   <b>같은 갈래끼리는 더하고, 다른 갈래끼리는 곱한다.</b> 그러면
    ///   ① 한 갈래에 몰아줄수록 수확이 저절로 체감하고
    ///   ② 새 갈래를 처음 여는 순간이 <b>계단</b>이 된다.
    ///   밸런싱 손잡이가 「갈래가 몇 개냐」 하나로 압축되는 게 이 구조의 값어치다.
    /// </summary>
    public enum IdleHeroAxis
    {
        /// <summary>때리는 힘.</summary>
        Damage = 0,

        /// <summary>때리는 빠르기.</summary>
        Speed = 1,

        /// <summary>기지 산출.</summary>
        Base = 2,

        /// <summary>장비 떨구기.</summary>
        Drop = 3,
    }

    /// <summary>
    /// 영웅 한 종류의 <b>변하지 않는 몫</b> — 뽑기 전에 이미 정해져 있는 것.
    ///
    /// ★ 종류(Kind)와 <b>가진 것</b>(<see cref="IdleHeroOwned"/>)을 나눈다.
    ///   저장에는 「몇 번을 몇 ★ 로 가졌나」만 들어가고, 성능표는 코드·SO 에 있다 —
    ///   그래야 수치를 고쳐도 남의 저장이 안 깨진다.
    /// </summary>
    public readonly struct IdleHeroKind
    {
        public IdleHeroKind(int id, string name, IdleHeroAxis axis, IdleHeroGrade grade, int sides)
        {
            Id = id;
            Name = name;
            Axis = axis;
            Grade = grade;
            Sides = sides;
        }

        public int Id { get; }

        /// <summary>사람이 부르는 이름. 세계관 정하기 전이라 생김새에서 따온다.</summary>
        public string Name { get; }

        /// <summary>이 영웅이 미는 축.</summary>
        public IdleHeroAxis Axis { get; }

        public IdleHeroGrade Grade { get; }

        /// <summary>몇 각형으로 그리나 — 화면이 이 수만 알면 된다.</summary>
        public int Sides { get; }
    }

    /// <summary>
    /// 영웅 등급. 뽑을 때의 확률과 몫의 크기를 함께 정한다.
    ///
    /// ★ 네 단계 (업계 관측 3~9, 인디 기본값 4). 더 늘리면 <b>차이가 안 보이고</b>,
    ///   줄이면 뽑기의 오르내림이 없어진다.
    /// </summary>
    public enum IdleHeroGrade
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legend = 3,
    }

    /// <summary>
    /// <b>가진 영웅 하나</b> — 몇 ★ 이고 조각이 얼마나 쌓였나.
    ///
    /// ★ 중복이 <b>꽝이 되지 않게</b> 하는 것이 수집형의 생사다
    ///   (<c>refs/korean-idle-gacha.md</c>: ★ 상한 포화 뒤의 배출구 부재 = 고인물 이탈 지점).
    ///   그래서 중복은 ★ 로 오르고, ★ 이 상한에 닿으면 <b>조각</b>으로 남아 다른 데 쓰인다.
    /// </summary>
    [Serializable]
    public struct IdleHeroOwned
    {
        /// <summary><see cref="IdleHeroKind.Id"/>.</summary>
        public int Id;

        /// <summary>승급 단계 (0 부터). 중복을 먹여 올린다.</summary>
        public int Stars;

        /// <summary>★ 을 올리려고 모으는 중인 중복 수.</summary>
        public int Copies;

        /// <summary>
        /// 골드로 올리는 레벨 (economy.md 표 3 첫 줄, U4).
        ///
        /// ★ ★ 과 나눈 이유. ★ 은 중복으로 올리고 환생을 넘어가지만, 레벨은 골드로 올리고
        ///   환생 때 사라진다. 매 판 다시 키우는 것이 있어야 환생이 되감기가 아니라 다시 시작
        /// </summary>
        public int Level;

        public int DamageLevel;

        public int AttackSpeedLevel;

        public int MaxHealthLevel;

        public int DefenseLevel;

        public int CriticalChanceLevel;

        public int CriticalDamageLevel;

        public int RecoveryLevel;

        public IdleHeroOwned(int id)
        {
            Id = id;
            Stars = 0;
            Copies = 0;
            Level = 0;
            DamageLevel = 0;
            AttackSpeedLevel = 0;
            MaxHealthLevel = 0;
            DefenseLevel = 0;
            CriticalChanceLevel = 0;
            CriticalDamageLevel = 0;
            RecoveryLevel = 0;
        }

        public int StatLevel(IdleUpgradeKind kind)
        {
            switch (kind)
            {
                case IdleUpgradeKind.Damage: return DamageLevel;
                case IdleUpgradeKind.AttackSpeed: return AttackSpeedLevel;
                case IdleUpgradeKind.MaxHealth: return MaxHealthLevel;
                case IdleUpgradeKind.Defense: return DefenseLevel;
                case IdleUpgradeKind.CriticalChance: return CriticalChanceLevel;
                case IdleUpgradeKind.CriticalDamage: return CriticalDamageLevel;
                default: return RecoveryLevel;
            }
        }

        public void SetStatLevel(IdleUpgradeKind kind, int level)
        {
            switch (kind)
            {
                case IdleUpgradeKind.Damage: DamageLevel = level; break;
                case IdleUpgradeKind.AttackSpeed: AttackSpeedLevel = level; break;
                case IdleUpgradeKind.MaxHealth: MaxHealthLevel = level; break;
                case IdleUpgradeKind.Defense: DefenseLevel = level; break;
                case IdleUpgradeKind.CriticalChance: CriticalChanceLevel = level; break;
                case IdleUpgradeKind.CriticalDamage: CriticalDamageLevel = level; break;
                default: RecoveryLevel = level; break;
            }
        }
    }
}
