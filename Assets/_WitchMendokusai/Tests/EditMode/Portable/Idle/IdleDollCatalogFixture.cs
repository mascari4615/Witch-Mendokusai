using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
    [SetUpFixture]
    public sealed class IdleDollCatalogFixture
    {
        [OneTimeSetUp]
        public void ConfigureCatalog()
        {
            IdleDolls.Configure(new IdleDollCatalog(new[]
            {
                new IdleDollKind(0, "세모", IdleDollAxis.Damage, IdleDollGrade.Common, 3),
                new IdleDollKind(1, "네모", IdleDollAxis.Base, IdleDollGrade.Common, 4),
                new IdleDollKind(2, "다섯모", IdleDollAxis.Drop, IdleDollGrade.Common, 5),
                new IdleDollKind(3, "여섯모", IdleDollAxis.Speed, IdleDollGrade.Common, 6),
                new IdleDollKind(4, "쐐기", IdleDollAxis.Damage, IdleDollGrade.Rare, 3),
                new IdleDollKind(5, "벽돌", IdleDollAxis.Base, IdleDollGrade.Rare, 4),
                new IdleDollKind(6, "별모", IdleDollAxis.Drop, IdleDollGrade.Rare, 5),
                new IdleDollKind(7, "톱니", IdleDollAxis.Speed, IdleDollGrade.Rare, 7),
                new IdleDollKind(8, "칼날", IdleDollAxis.Damage, IdleDollGrade.Epic, 3),
                new IdleDollKind(9, "성채", IdleDollAxis.Base, IdleDollGrade.Epic, 6),
                new IdleDollKind(10, "그물", IdleDollAxis.Drop, IdleDollGrade.Epic, 8),
                new IdleDollKind(11, "회오리", IdleDollAxis.Speed, IdleDollGrade.Epic, 9),
                new IdleDollKind(12, "송곳", IdleDollAxis.Damage, IdleDollGrade.Legend, 3),
                new IdleDollKind(13, "고리", IdleDollAxis.Base, IdleDollGrade.Legend, 10),
                new IdleDollKind(14, "여울", IdleDollAxis.Drop, IdleDollGrade.Legend, 11),
                new IdleDollKind(15, "번개", IdleDollAxis.Speed, IdleDollGrade.Legend, 12),
            }));
        }
    }
}
