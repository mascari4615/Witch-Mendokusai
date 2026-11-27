using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public class DevContentTest
	{
		[TearDown]
		public void ResetOverride() => DevContent.OverrideIsShownForTest(null);

		// 릴리스 판정 (false) 과 개발 판정 (true) 둘 다 이 판정만 따라감. 에디터는 Debug.isDebugBuild 가 늘 true 라 덮어써야 릴리스 쪽을 시험함
		[TestCase(false)]
		[TestCase(true)]
		public void OverrideDecidesInTests(bool isShown)
		{
			DevContent.OverrideIsShownForTest(isShown);
			Assert.AreEqual(isShown, DevContent.IsShown);
		}

	}
}
