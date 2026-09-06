using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
    [SetUpFixture]
    public sealed class IdleHeroCatalogFixture
    {
        [OneTimeSetUp]
        public void ConfigureCatalog()
        {
            IdleHeroes.Configure(new IdleHeroCatalog(new[]
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
            }));
        }
    }
}
