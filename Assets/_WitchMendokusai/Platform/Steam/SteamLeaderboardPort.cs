using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

namespace WitchMendokusai
{
	/// <summary>
	/// Steam 순위표 쓰기 창구. Facepunch.Steamworks 호출만, 판단은 LeaderboardRelay 몫.
	///
	/// - 순위표가 없으면 연결표의 정렬, 표시로 만듦 (FindOrCreate). Steamworks 웹에서 미리 만들 필요 없음
	/// - 올리기는 SubmitScoreAsync: Steam 이 순위표 정렬 기준으로 더 좋은 값만 남김
	/// - Steam 올리기 제한 (10분에 10회) 을 넘기 전에 기다림. 한 번에 하나는 LeaderboardRelay 가 보장
	/// </summary>
	public sealed class SteamLeaderboardPort : ILeaderboardPlatformPort
	{
		private readonly Dictionary<string, SteamLeaderboardLink> linkByName = new();
		private readonly Dictionary<string, Leaderboard> boardByName = new();
		private readonly Queue<DateTime> recentUploads = new();

		// Steam 이 정한 사용자당 올리기 제한. 조정할 게임 수치가 아님
		private const int UPLOADS_PER_WINDOW = 10;
		private static readonly TimeSpan UPLOAD_WINDOW = TimeSpan.FromMinutes(10);

		public SteamLeaderboardPort(IReadOnlyList<SteamLeaderboardLink> links)
		{
			foreach (SteamLeaderboardLink link in links)
			{
				linkByName.Add(link.LeaderboardName, link);
			}
		}

		public async Task<bool> SubmitBest(string leaderboardName, int score)
		{
			if (boardByName.TryGetValue(leaderboardName, out Leaderboard board) == false)
			{
				SteamLeaderboardLink link = linkByName[leaderboardName];
				LeaderboardSort sort = link.LowerIsBetter ? LeaderboardSort.Ascending : LeaderboardSort.Descending;
				LeaderboardDisplay display = link.DisplayAsSeconds ? LeaderboardDisplay.TimeSeconds : LeaderboardDisplay.Numeric;
				Leaderboard? found = await SteamUserStats.FindOrCreateLeaderboardAsync(leaderboardName, sort, display);
				if (found.HasValue == false)
				{
					return false;
				}
				board = found.Value;
				boardByName[leaderboardName] = board;
			}
			await WaitForUploadSlot();
			recentUploads.Enqueue(DateTime.UtcNow);
			LeaderboardUpdate? update = await board.SubmitScoreAsync(score);
			return update.HasValue;
		}

		private async Task WaitForUploadSlot()
		{
			while (recentUploads.Count > 0 && DateTime.UtcNow - recentUploads.Peek() >= UPLOAD_WINDOW)
			{
				recentUploads.Dequeue();
			}
			if (recentUploads.Count < UPLOADS_PER_WINDOW)
			{
				return;
			}
			TimeSpan wait = recentUploads.Peek() + UPLOAD_WINDOW - DateTime.UtcNow;
			if (wait > TimeSpan.Zero)
			{
				await Task.Delay(wait);
			}
			recentUploads.Dequeue();
		}
	}
}
