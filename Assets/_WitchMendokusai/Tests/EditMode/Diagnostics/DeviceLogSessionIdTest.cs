using System;
using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 로그 세션 이름. 이 이름이 서버에서 *파일명*이 되고, 목록에서
    /// 「어느 빌드의 실행인가」를 눈으로 가르는 첫 단서다.
    /// </summary>
    public class DeviceLogSessionIdTest
    {
        private static readonly DateTime WHEN = new DateTime(2026, 8, 6, 3, 12, 45);

        [Test]
        public void 빌드_번호가_이름에_들어간다()
        {
            Assert.AreEqual("android-b412-20260806-031245",
                DeviceLogRelay.ComposeSessionId("Android", 412, WHEN));
        }

        [Test]
        public void 손으로_구운_빌드는_번호를_뺀다()
        {
            Assert.AreEqual("android-20260806-031245",
                DeviceLogRelay.ComposeSessionId("Android", 0, WHEN));
        }

        [Test]
        public void 파일명에_못_쓰는_글자는_붙임표로_바뀐다()
        {
            StringAssert.StartsWith("windows-editor-", DeviceLogRelay.ComposeSessionId("Windows Editor", 0, WHEN));
            Assert.IsFalse(DeviceLogRelay.ComposeSessionId("A/B\\C", 0, WHEN).Contains("/"));
        }

        [Test]
        public void 플랫폼을_모르면_기본_이름을_쓴다()
        {
            StringAssert.StartsWith("device-", DeviceLogRelay.ComposeSessionId(null, 0, WHEN));
            StringAssert.StartsWith("device-", DeviceLogRelay.ComposeSessionId("", 0, WHEN));
        }
    }
}
