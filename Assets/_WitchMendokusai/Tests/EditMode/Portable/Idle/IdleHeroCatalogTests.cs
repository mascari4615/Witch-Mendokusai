using System;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests.Idle
{
    public sealed class IdleHeroCatalogTests
    {
        [Test]
        public void CatalogKeepsDefinitionValues()
        {
            IdleHeroCatalog catalog = new IdleHeroCatalog(new[]
            {
                new IdleHeroKind(0, "첫째", IdleHeroAxis.Damage, IdleHeroGrade.Common, 3),
                new IdleHeroKind(1, "둘째", IdleHeroAxis.Speed, IdleHeroGrade.Rare, 7),
            });

            Assert.AreEqual(2, catalog.Count);
            Assert.AreEqual("둘째", catalog.KindOf(1).Name);
            Assert.AreEqual(IdleHeroGrade.Rare, catalog.KindOf(1).Grade);
        }

        [Test]
        public void CatalogRejectsSaveBreakingIdGaps()
        {
            Assert.Throws<ArgumentException>(() => new IdleHeroCatalog(new[]
            {
                new IdleHeroKind(1, "잘못된 첫 ID", IdleHeroAxis.Damage, IdleHeroGrade.Common, 3),
            }));
        }
    }
}
