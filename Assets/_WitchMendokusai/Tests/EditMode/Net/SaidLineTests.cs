using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 사람이 <b>한 말</b>을 세계가 어떻게 받아들이나 (TASK-WM-250).
	/// 말은 사람이 직접 짓는 유일한 것이라, 세계가 안 보면 한 줄로 남의 화면을 부술 수 있다.
	/// </summary>
	public class SaidLineTests
	{
		[Test]
		public void 보통_말은_그대로_간다()
		{
			Assert.AreEqual("같이 갈래?", SaidLine.Clean("같이 갈래?"));
		}

		[Test]
		public void 앞뒤_공백은_버린다()
		{
			Assert.AreEqual("여기 있어", SaidLine.Clean("   여기 있어   "));
		}

		[Test]
		public void 빈_말은_말이_아니다()
		{
			Assert.IsNull(SaidLine.Clean(""), "빈 줄이 남에게 가면 그건 소음 장치가 된다");
			Assert.IsNull(SaidLine.Clean("     "));
			Assert.IsNull(SaidLine.Clean(null));
			Assert.IsNull(SaidLine.Clean("\n\n\t"));
		}

		[Test]
		public void 줄바꿈으로_남의_화면을_밀어_올리지_못한다()
		{
			string pushed = "위\n\n\n\n\n아래";

			Assert.AreEqual("위 아래", SaidLine.Clean(pushed),
				"줄바꿈이 그대로 가면 한 줄로 남의 화면을 통째로 밀어 올린다");
		}

		[Test]
		public void 보이지_않는_글자도_한_칸으로()
		{
			Assert.AreEqual("가 나", SaidLine.Clean("가\u0000\u0007나"));
		}

		[Test]
		public void 너무_길면_자른다()
		{
			string tooLong = new string('가', SaidLine.LONGEST + 50);

			Assert.AreEqual(SaidLine.LONGEST, SaidLine.Clean(tooLong).Length);
		}

		[Test]
		public void 딱_맞는_길이는_안_자른다()
		{
			string exact = new string('나', SaidLine.LONGEST);

			Assert.AreEqual(exact, SaidLine.Clean(exact));
		}
	}
}
