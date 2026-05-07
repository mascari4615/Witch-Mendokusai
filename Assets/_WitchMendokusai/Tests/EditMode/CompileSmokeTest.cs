using NUnit.Framework;

namespace WitchMendokusai.EditMode.Tests
{
	/// <summary>
	/// Compile smoke test — Unity Editor 가 본 test 까지 도달했다는 것만으로 main assembly 컴파일 통과 보장.
	///
	/// 컴파일 게이트 효과는 *test 자체* 의 ref 가 아니라 **Unity Editor 시작 흐름** 에서 옴.
	/// main assembly 빌드 실패 = Editor reload 실패 = test runner 시작 못 함 = `game-ci/unity-test-runner` action fail.
	///
	/// 즉 본 test 가 단순 `Assert.Pass` 라도 main syntax error / 타입 mismatch / missing symbol 자동 차단.
	/// </summary>
	public class CompileSmokeTest
	{
		[Test]
		public void EditorAssembliesLoad()
		{
			Assert.Pass("Unity Editor reached test runner — main assembly compiled successfully.");
		}
	}
}
