using System;
using System.Globalization;
using System.Threading;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 시험이 「얼마나 기다려 주나」를 **한 곳에서** 정한다.
	///
	/// ★ 왜 있나 (2026-08-12): 시험마다 `TimeSpan.FromSeconds(10)` 이 손으로 박혀 있었다.
	///   내 기계에서는 넉넉한 값이지만 공유 러너(2코어 VM)에서는 아니다 — 소켓 왕복 다섯 개가
	///   그 선을 넘겨 `asmdef boundary` 게이트가 스물다섯 판 내내 빨갰다.
	///   기다리는 시간은 **제품 규격이 아니라 환경 값**이다. 기계가 느리면 같이 늘어나야 한다.
	///
	///   숫자를 두 벌 두지 않는다 — 시험은 여전히 「몇 초쯤이면 와야 한다」를 자기 자리에 적고,
	///   여기서는 그 값에 **이 환경의 배수**만 곱한다. 배수는 `WM_TEST_SLACK` 으로 덮을 수 있고,
	///   CI 에서는 기본 4배다(실측: 로컬 전체 36초 ↔ CI 같은 묶음 1분 25초에 다섯 개가 잘림).
	/// </summary>
	internal static class TestTimeout
	{
		private const double DEFAULT_LOCAL_SLACK = 1.0;
		private const double DEFAULT_CI_SLACK = 4.0;

		private static readonly double SLACK = ReadSlack();

		private static double ReadSlack()
		{
			string fromEnvironment = Environment.GetEnvironmentVariable("WM_TEST_SLACK");
			if (string.IsNullOrWhiteSpace(fromEnvironment) == false
				&& double.TryParse(fromEnvironment, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
				&& parsed > 0)
			{
				return parsed;
			}

			/* 깃허브 액션은 `CI=true` 를 심는다. 다른 러너도 대개 같은 관습을 따른다. */
			string ci = Environment.GetEnvironmentVariable("CI");
			bool onCi = string.IsNullOrWhiteSpace(ci) == false && ci.Equals("false", StringComparison.OrdinalIgnoreCase) == false;
			return onCi ? DEFAULT_CI_SLACK : DEFAULT_LOCAL_SLACK;
		}

		/// <summary>「내 기계에서 <paramref name="seconds"/> 초면 온다」 — 느린 기계에서는 그만큼 더 기다린다.</summary>
		internal static CancellationTokenSource After(double seconds)
		{
			return new CancellationTokenSource(TimeSpan.FromSeconds(seconds * SLACK));
		}
	}
}
