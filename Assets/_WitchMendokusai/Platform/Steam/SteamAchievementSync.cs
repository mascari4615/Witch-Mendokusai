using Steamworks;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 업적을 Steam 으로 옮기는 배선. SteamService 가 소유, 매 프레임 Tick.
	///
	/// - 시작 조건 둘: Steam 업적 상태 수신 (OnUserStatsReceived), 게임 업적 장부 등록 (세이브 로드 끝)
	///   장부 등록 전 구독은 이벤트 통로 교체 (RootLifetimeScope) 전이라 놓칠 수 있어 기다림
	/// - 시작하면: 장부 전체 재동기화 한 번, 이후 QuestCompletedEvent 구독
	/// - 한 방향: Steam 업적 상태를 읽어 게임에 반영하지 않음
	/// </summary>
	public sealed class SteamAchievementSync
	{
		private readonly SteamSettings settings;
		private AchievementRelay relay;
		private bool statsReady;

		public SteamAchievementSync(SteamSettings settings)
		{
			this.settings = settings;
			SteamUserStats.OnUserStatsReceived += OnUserStatsReceived;
			// 최신 Steam 클라이언트는 실행 전에 동기화를 끝내 둠 (요청 불필요). 요청이 거절되면 이미 준비된 것으로 봄
			if (SteamUserStats.RequestCurrentStats() == false)
			{
				statsReady = true;
			}
		}

		public bool IsStarted => relay != null;

		public void Tick()
		{
			if (IsStarted || statsReady == false)
			{
				return;
			}
			IAchievementLedger ledger = AchievementLedgerBridge.Current;
			if (ledger == null)
			{
				return;
			}

			AchievementRelay startingRelay = new AchievementRelay(settings.BuildAchievementMap(), new SteamAchievementPort());
			EventBusBridge.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
			relay = startingRelay;
			int relayed = relay.Resync(ledger.CompletedAchievementIds);
			Debug.Log("[Steam] 업적 연결 시작. 연결표 " + settings.Achievements.Count + "개, 재동기화 " + relayed + "개");
		}

		public void Stop()
		{
			SteamUserStats.OnUserStatsReceived -= OnUserStatsReceived;
			if (IsStarted == false)
			{
				return;
			}
			EventBusBridge.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
			relay = null;
		}

		private void OnUserStatsReceived(SteamId steamId, Result result)
		{
			if (steamId == SteamClient.SteamId && result == Result.OK)
			{
				statsReady = true;
			}
		}

		private void OnQuestCompleted(QuestCompletedEvent evt)
		{
			if (evt.Type != QuestType.Achievement)
			{
				return;
			}
			relay.OnAchievementCompleted(evt.QuestSOID);
		}
	}
}
