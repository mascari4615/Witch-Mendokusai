using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 「로그가 나가고 있나」 한 줄. 이 문장이 애매하면 무음 실패가 그대로 남는다:
    /// 로그가 안 오는 게 조용한 실행인지 막힌 전송인지 구분되어야 한다.
    /// </summary>
    public class DeviceLogStatusTest
    {
        [Test]
        public void 잘_나가면_보낸_줄_수를_말한다()
        {
            Assert.AreEqual("보냄 128줄", DeviceLogStatus.Line(128, 0, 0, 0));
        }

        [Test]
        public void 아직_못_보낸_줄이_있으면_대기로_따로_말한다()
        {
            StringAssert.Contains("대기 7줄", DeviceLogStatus.Line(10, 7, 0, 0));
        }

        [Test]
        public void 인증에_막히면_이유를_찍어_말한다()
        {
            string line = DeviceLogStatus.Line(0, 12, 3, 401);
            StringAssert.Contains("막힘", line);
            StringAssert.Contains("401", line);
            StringAssert.Contains(DeviceLogStatus.HTTP_UNAUTHORIZED_HINT, line);
            StringAssert.Contains("실패 3회", line);
        }

        [Test]
        public void 응답_자체가_없으면_그렇게_말한다()
        {
            StringAssert.Contains("응답 없음", DeviceLogStatus.Line(0, 5, 2, 0));
        }

        [Test]
        public void 다른_실패_코드는_숫자_그대로()
        {
            StringAssert.Contains("500", DeviceLogStatus.Line(3, 1, 1, 500));
        }

        [Test]
        public void 릴레이가_안_켜졌으면_꺼졌다고_말한다()
        {
            StringAssert.Contains("꺼짐", DeviceLogStatus.OffLine());
        }

        [Test]
        public void 막힌_상태에서도_지금까지_보낸_양은_계속_보인다()
        {
            StringAssert.Contains("보냄 42줄", DeviceLogStatus.Line(42, 0, 1, 401));
        }
    }
}
