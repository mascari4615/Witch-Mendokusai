using System;
using NUnit.Framework;
using WitchMendokusai.EditorTools;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 빌드 시점에 굽는 값. CI 러너는 UTC 라, 시각을 그냥 적으면
    /// 9시간 어긋난 「구운 때」가 폰 화면에 뜬다. 그 함정만 결정적으로 못 박는다.
    /// (git·환경변수 의존 부분은 러너마다 달라서 여기서 단정하지 않는다.)
    /// </summary>
    public class BuildInfoCollectTest
    {
        [Test]
        public void 구운_시각은_한국_시간으로_적힌다()
        {
            DateTime expected = DateTime.UtcNow.AddHours(9);
            string stamp = WMBuildInfoBuildStep.KstNow();

            Assert.AreEqual(16, stamp.Length, $"형식이 'yyyy-MM-dd HH:mm' 이어야 한다: {stamp}");
            StringAssert.StartsWith(expected.ToString("yyyy-MM-dd"), stamp);
        }

        [Test]
        public void 개발_빌드와_릴리스_빌드가_통로_이름으로_갈린다()
        {
            Assert.AreEqual("dev", WMBuildInfoBuildStep.Collect(true, "Android").channel);
            Assert.AreEqual("release", WMBuildInfoBuildStep.Collect(false, "Android").channel);
        }

        [Test]
        public void 대상_플랫폼이_그대로_실린다()
        {
            Assert.AreEqual("Android", WMBuildInfoBuildStep.Collect(true, "Android").platform);
        }

        [Test]
        public void 유니티_버전은_비지_않는다()
        {
            Assert.IsFalse(string.IsNullOrEmpty(WMBuildInfoBuildStep.Collect(true, "Android").unityVersion));
        }
    }
}
