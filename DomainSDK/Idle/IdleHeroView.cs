using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 가진 영웅 하나가 화면에 보이는 모습 (TASK-WM-406).
    /// </summary>
    public readonly struct IdleHeroView
    {
        public IdleHeroView(int id, string name, IdleHeroGrade grade, IdleHeroAxis axis, int sides,
            int stars, int copies, int copiesForNextStar, bool inParty, double ownedShare,
            int level, double levelCost, bool canRaiseLevel, bool canRaiseStat)
        {
            Level = level;
            LevelCost = levelCost;
            CanRaiseLevel = canRaiseLevel;
            CanRaiseStat = canRaiseStat;
            Id = id;
            Name = name;
            Grade = grade;
            Axis = axis;
            Sides = sides;
            Stars = stars;
            Copies = copies;
            CopiesForNextStar = copiesForNextStar;
            InParty = inParty;
            OwnedShare = ownedShare;
        }

        public int Id { get; }
        public string Name { get; }
        public IdleHeroGrade Grade { get; }
        public IdleHeroAxis Axis { get; }

        /// <summary>몇 각형으로 그리나.</summary>
        public int Sides { get; }

        public int Stars { get; }

        /// <summary>골드로 올린 레벨 (economy.md 표 3). 환생 때 0 으로</summary>
        public int Level { get; }

        /// <summary>다음 한 칸에 드는 골드</summary>
        public double LevelCost { get; }

        /// <summary>지금 올릴 수 있나. 판정은 코어가 한다</summary>
        public bool CanRaiseLevel { get; }

        public bool CanRaiseStat { get; }

        /// <summary>다음 ★ 까지 모은 중복.</summary>
        public int Copies { get; }

        public int CopiesForNextStar { get; }

        /// <summary>지금 내보내고 있나.</summary>
        public bool InParty { get; }

        /// <summary>들고만 있어도 붙는 몫(비율) — 「이 얼굴이 지금 얼마나 보태나」.</summary>
        public double OwnedShare { get; }
    }
}


