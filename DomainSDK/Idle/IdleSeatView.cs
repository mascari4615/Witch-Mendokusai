using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 한 자리가 화면에 보이는 모습 (V2 부대층) — 0번 = 나, 1~3 = 파티 자리.
    /// </summary>
    public readonly struct IdleSeatView
    {
        public IdleSeatView(int seat, bool taken, bool standing, double healthRatio, double reviveRatio,
            int heroId, IdleHeroGrade grade)
        {
            Seat = seat;
            Taken = taken;
            Standing = standing;
            HealthRatio = healthRatio;
            ReviveRatio = reviveRatio;
            HeroId = heroId;
            Grade = grade;
        }

        public int Seat { get; }

        /// <summary>누가 있나 — 빈 파티 자리는 거짓.</summary>
        public bool Taken { get; }

        /// <summary>서 있나 — 거짓이면 쓰러져 부활을 기다린다.</summary>
        public bool Standing { get; }

        /// <summary>남은 체력 비율(0~1).</summary>
        public double HealthRatio { get; }

        /// <summary>부활까지 찬 비율(0~1) — 쓰러졌을 때만 뜻이 있다.</summary>
        public double ReviveRatio { get; }

        /// <summary>앉은 영웅 번호 — 나(0번)이거나 빈 자리면 -1.</summary>
        public int HeroId { get; }

        /// <summary>그 영웅의 등급 — 화면이 색으로 옮긴다.</summary>
        public IdleHeroGrade Grade { get; }
    }
}


