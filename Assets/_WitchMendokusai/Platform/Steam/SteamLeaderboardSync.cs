namespace WitchMendokusai
{
	/// <summary>
	/// 게임 기록판 (GameRecords) 을 Steam 순위표로 옮기는 배선. SteamService 가 소유.
	///
	/// - 시작: 이미 알려진 기록 전체 재동기화, 이후 BestReported 구독
	/// - 알림이 올 때마다 전체 재동기화: 성공한 값은 중계가 거르므로 다시 안 감, 앞서 실패한 기록은 여기서 다시 시도
	/// - 한 방향: Steam 순위를 읽어 게임에 반영하지 않음
	/// </summary>
	public sealed class SteamLeaderboardSync
	{
		private readonly LeaderboardRelay relay;

		public SteamLeaderboardSync(SteamSettings settings)
		{
			relay = new LeaderboardRelay(settings.BuildLeaderboardMap(), new SteamLeaderboardPort(settings.Leaderboards));
			GameRecords.BestReported += OnBestReported;
			relay.Resync(GameRecords.Bests);
		}

		public void Stop() => GameRecords.BestReported -= OnBestReported;

		private void OnBestReported(string recordKey, int best) => relay.Resync(GameRecords.Bests);
	}
}
