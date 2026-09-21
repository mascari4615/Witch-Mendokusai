using System;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests.Idle
{
    public sealed class IdleDollCatalogTests
    {
        [Test]
        public void CatalogKeepsDefinitionValues()
        {
            IdleDollCatalog catalog = new IdleDollCatalog(new[]
            {
                new IdleDollKind(0, "첫째", IdleDollAxis.Damage, IdleDollGrade.Common, 3),
                new IdleDollKind(1, "둘째", IdleDollAxis.Speed, IdleDollGrade.Rare, 7),
            });

            Assert.AreEqual(2, catalog.Count);
            Assert.AreEqual("둘째", catalog.KindOf(1).Name);
            Assert.AreEqual(IdleDollGrade.Rare, catalog.KindOf(1).Grade);
        }

        [Test]
        public void CatalogRejectsSaveBreakingIdGaps()
        {
            Assert.Throws<ArgumentException>(() => new IdleDollCatalog(new[]
            {
                new IdleDollKind(1, "잘못된 첫 ID", IdleDollAxis.Damage, IdleDollGrade.Common, 3),
            }));
        }
    }
}
