using System;
using System.IO;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 팔 게임이 <b>남의 서버를 안 부르는가</b> (TASK-WM-406).
	///
	/// ★ 2026-08-16 실측: 구운 방치형 exe 를 켜니 <b>`[DeviceLog] 전송 실패 (401)`</b> 이 찍혔다.
	///   본편 진단 장치(`DeviceLogRelay` · `BuildStampOverlay`)는 씬에 아무것도 안 놔도
	///   `[RuntimeInitializeOnLoadMethod]` 로 스스로 뜬다. 그대로 팔면 산 사람의 기계가
	///   내 서버를 두드리고, 화면엔 개발용 도장이 뜬다.
	///   → 몸통은 남기고 <b>스스로 뜨게 하는 표지만</b> `#if !WM_IDLE` 로 감쌌다.
	///
	/// ⚠ 이 판이 지키는 것은 그 <b>한 줄</b>이다. 지워져도 컴파일은 되고 시험도 초록이며
	///   빌드도 성공한다 — 켜야 안다. 켜 보는 검사(`wm-idle-smoke.ps1`)는 exe 를 굽고 나서야 돈다.
	///   그래서 여기서 <b>소스를 직접 읽어</b> 먼저 잡는다. 에디터에는 `WM_IDLE` 표식이 없어
	///   리플렉션으로는 이 조건을 볼 수 없다(속성이 항상 붙어 보인다) — 그래서 텍스트로 본다.
	/// </summary>
	public sealed class IdleNoTelemetryTests
	{
		private const string IDLE_GUARD = "#if !WM_IDLE";
		private const string AUTO_INSTALL = "[RuntimeInitializeOnLoadMethod";

		private const string RELAY_PATH = "_WitchMendokusai/Core/Diagnostics/DeviceLogRelay.cs";
		private const string STAMP_PATH = "_WitchMendokusai/Core/Diagnostics/BuildStampOverlay.cs";
		private const string BUILD_PATH = "_WitchMendokusai/Idle/Editor/IdlePlayerBuild.cs";

		/// <summary>★ 로그 릴레이는 방치형 빌드에서 스스로 뜨지 않는다.</summary>
		[Test]
		public void DeviceLogRelay_DoesNotSelfInstall_InIdleBuild()
		{
			AssertGuardedAutoInstall(RELAY_PATH, "기기 로그 릴레이");
		}

		/// <summary>★ 빌드 도장 표시기도 마찬가지 — 산 사람 화면에 개발용 도장이 뜨면 안 된다.</summary>
		[Test]
		public void BuildStampOverlay_DoesNotSelfInstall_InIdleBuild()
		{
			AssertGuardedAutoInstall(STAMP_PATH, "빌드 도장 표시기");
		}

		/// <summary>★ 가드가 있어도 표식이 안 붙으면 그대로 뜬다 — 빌드가 표식을 붙이는지 본다.</summary>
		[Test]
		public void IdleBuild_AddsIdleDefine()
		{
			string source = ReadSource(BUILD_PATH);

			Assert.That(
				source,
				Does.Contain("extraScriptingDefines"),
				"방치형 빌드가 WM_IDLE 표식을 안 붙인다 — 표식이 없으면 본편 진단 장치가 그대로 실린다");
			Assert.That(
				source,
				Does.Contain("IDLE_DEFINE"),
				"WM_IDLE 표식 상수가 빌드에서 사라졌다");
		}

		/// <summary>
		/// 자동 설치 표지 바로 앞이 `#if !WM_IDLE` 인지 본다.
		/// 파일 어딘가에 둘 다 있는 것으로는 부족하다 — <b>그 표지를 감싸고 있어야</b> 한다.
		/// </summary>
		private static void AssertGuardedAutoInstall(string relativePath, string label)
		{
			string source = ReadSource(relativePath);

			int install = source.IndexOf(AUTO_INSTALL, System.StringComparison.Ordinal);
			Assert.That(
				install,
				Is.GreaterThanOrEqualTo(0),
				label + " 에서 자동 설치 표지를 못 찾았다 — 이 판의 전제가 바뀌었다면 시험도 고칠 것");

			int guard = source.LastIndexOf(IDLE_GUARD, install, System.StringComparison.Ordinal);
			Assert.That(
				guard,
				Is.GreaterThanOrEqualTo(0),
				label + " 의 자동 설치가 `#if !WM_IDLE` 밖에 있다 — 팔 게임이 다시 남의 서버를 부른다");

			// 가드와 표지 사이에 `#endif` 가 끼면 감싼 게 아니다.
			string between = source.Substring(guard, install - guard);
			Assert.That(
				between,
				Does.Not.Contain("#endif"),
				label + " 의 `#if !WM_IDLE` 가 자동 설치 표지를 감싸지 않는다 (사이에서 닫혔다)");
		}

		/// <summary>
		/// 소스를 읽는다 — <b>엔진 없이</b>.
		///
		/// ⚠ 전에는 <c>Application.dataPath</c> 를 썼다. 그래서 이 판은 <b>유니티 러너에서만</b>
		///   돌았고, 이 저장소의 1분짜리 되먹임 고리(엔진 밖 시험)에서는 <b>한 번도 안 돌았다</b>
		///   (실측 2026-08-17 — 목록에 아예 없었다). 지키는 것이 「팔 게임이 남의 서버를
		///   안 부른다」인데, 정작 그걸 지키는 감시가 잠들어 있었다.
		///   저장소 뿌리는 폴더를 거슬러 올라가 찾으면 되고, 그러면 양쪽에서 다 돈다.
		/// </summary>
		private static string ReadSource(string relativePath)
		{
			string full = Path.Combine(FindAssetsRoot(), relativePath);
			Assert.That(File.Exists(full), Is.True, "소스를 못 찾았다: " + full);
			return File.ReadAllText(full);
		}

		private static string FindAssetsRoot()
		{
			// ★ 유니티 안에서는 dataPath 가 곧 <프로젝트>/Assets 다 — 이걸 먼저 본다 (TASK-WM-416).
			//   예전엔 AppContext.BaseDirectory 에서 위로 훑었는데, 테스트 러너에서 그 값은
			//   *에디터 설치 폴더*(…/Unity/Hub/Editor/…/Unity.exe)라 프로젝트를 영영 못 만났다.
			//   그래서 이 파일의 검사들이 「저장소 뿌리를 못 찾았다」로 늘 빨갰다(실측 2026-08-21).
			// ⚠ 엔진 밖(Portable/DomainSDK.Tests)에는 UnityEngine 이 없다. 이 줄이 그대로 있으면
			//   포터블 시험 전체가 컴파일에서 죽는다(실측 2026-08-30, ad58d6a7 이후 계속 빨강이었다).
			string dataPath = string.Empty;
#if UNITY_5_3_OR_NEWER
			dataPath = UnityEngine.Application.dataPath;

			if (string.IsNullOrEmpty(dataPath) == false
				&& Directory.Exists(Path.Combine(dataPath, "_WitchMendokusai")))
			{
				return dataPath;
			}
#endif

			// 유니티 밖(순수 dotnet)에서도 돌 수 있게 — 일하는 자리에서 위로 훑는다.
			DirectoryInfo at = new DirectoryInfo(Directory.GetCurrentDirectory());

			while (at != null)
			{
				string assets = Path.Combine(at.FullName, "Assets");

				if (Directory.Exists(Path.Combine(assets, "_WitchMendokusai")))
				{
					return assets;
				}

				at = at.Parent;
			}

			throw new DirectoryNotFoundException(
				"저장소 뿌리를 못 찾았다 — Assets/_WitchMendokusai 가 없다 "
				+ $"(dataPath={dataPath}, cwd={Directory.GetCurrentDirectory()})");
		}
	}
}
