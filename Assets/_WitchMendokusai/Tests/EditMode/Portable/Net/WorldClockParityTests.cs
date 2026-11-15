using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 세계 시간 흐름 속도: 게임 정본 WorldClockSO.asset 과 서버 WorldHost 상수가 같은 값인지.
	/// 서버는 유니티 자산 읽기 불가, 값 별도 보관. 둘의 어긋남 방지 (2026-11-15 독립 검토 지적)
	/// 엔진 밖 실행용, 소스와 자산 글자 직접 읽기
	/// </summary>
	public sealed class WorldClockParityTests
	{
		[Test]
		public void 게임과_서버의_분당_흐름이_같다()
		{
			string root = FindRepoRoot();
			string asset = File.ReadAllText(Path.Combine(root, "Assets/_WitchMendokusai/Core/Resources/WorldClockSO.asset"));
			string server = File.ReadAllText(Path.Combine(root, "Server/WM.Server/WorldHost.cs"));

			Match assetMatch = Regex.Match(asset, @"<MinutesPerRealSecond>k__BackingField:\s*([0-9.]+)");
			Match serverMatch = Regex.Match(server, @"MINUTES_PER_REAL_SECOND\s*=\s*([0-9.]+)f");
			Assert.That(assetMatch.Success, Is.True, "WorldClockSO.asset 에서 MinutesPerRealSecond 를 못 찾음");
			Assert.That(serverMatch.Success, Is.True, "WorldHost.cs 에서 MINUTES_PER_REAL_SECOND 를 못 찾음");

			float assetValue = float.Parse(assetMatch.Groups[1].Value, CultureInfo.InvariantCulture);
			float serverValue = float.Parse(serverMatch.Groups[1].Value, CultureInfo.InvariantCulture);
			Assert.That(serverValue, Is.EqualTo(assetValue));
		}

		[Test]
		public void 기본_달력은_설정_기본값과_같은_시작_시각()
		{
			WorldCalendar calendar = WorldCalendar.CreateDefault();

			Assert.That(calendar.Hour, Is.EqualTo(WorldCalendar.DEFAULT_START_HOUR));
			Assert.That(calendar.Minute, Is.EqualTo(0));
			Assert.That(calendar.HoursPerDay, Is.EqualTo(WorldCalendar.DEFAULT_HOURS_PER_DAY));
			Assert.That(new WorldSim().Calendar.Hour, Is.EqualTo(WorldCalendar.DEFAULT_START_HOUR));
		}

		private static string FindRepoRoot()
		{
			DirectoryInfo at = new DirectoryInfo(Directory.GetCurrentDirectory());
			while (at != null)
			{
				if (Directory.Exists(Path.Combine(at.FullName, "Assets", "_WitchMendokusai")))
					return at.FullName;
				at = at.Parent;
			}
			throw new DirectoryNotFoundException("저장소 뿌리를 못 찾음, cwd=" + Directory.GetCurrentDirectory());
		}
	}
}
