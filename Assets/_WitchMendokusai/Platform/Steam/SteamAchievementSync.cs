using Steamworks;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 업적을 Steam 으로 옮기는 배선. SteamService 가 소유, 매 프레임 Tick.
	///
	/// - 시작 조건: 게임 업적 장부 등록 (세이브 로드 끝). 그 전 구독은 이벤트 통로 교체 (RootLifetimeScope) 전이라 놓칠 수 있어 기다림
	/// - 시작하면: 장부 전체 재동기화 한 번, 이후 AchievementUnlockedEvent 구독
	/// - Steam 업적 상태 수신 (OnUserStatsReceived) 이 늦게 오면 재동기화를 한 번 더. 실패한 적기는 기록하지 않으므로 여기서 다시 시도됨
	///   (상태 요청 RequestCurrentStats 는 SDK 가 폐기, Steam 클라이언트가 실행 전에 동기화)
	/// - 한 방향: Steam 업적 상태를 읽어 게임에 반영하지 않음
	/// </summary>
	public sealed class SteamAchievementSync
	{
		private readonly SteamSettings settings;
		private AchievementRelay relay;

		public SteamAchievementSync(SteamSettings settings)
		{
			this.settings = settings;
			SteamUserStats.OnUserStatsReceived += OnUserStatsReceived;
		}

		public bool IsStarted => relay != null;

		public void Tick()
		{
			if (IsStarted)
			{
				return;
			}
			IAchievementLedger ledger = AchievementLedgerBridge.Current;
			if (ledger == null)
			{
				return;
			}

			AchievementRelay startingRelay = new AchievementRelay(settings.BuildAchievementMap(), new SteamAchievementPort());
			EventBusBridge.Subscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);
			relay = startingRelay;
			int relayed = relay.Resync(ledger.UnlockedSteps);
			Debug.Log("[Steam] 업적 연결 시작. 연결표 " + settings.Achievements.Count + "개, 재동기화 " + relayed + "개");
		}

		public void Stop()
		{
			SteamUserStats.OnUserStatsReceived -= OnUserStatsReceived;
			if (IsStarted == false)
			{
				return;
			}
			EventBusBridge.Unsubscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);
			relay = null;
		}

		private void OnUserStatsReceived(SteamId steamId, Result result)
		{
			if (IsStarted == false || steamId != SteamClient.SteamId || result != Result.OK)
			{
				return;
			}
			IAchievementLedger ledger = AchievementLedgerBridge.Current;
			if (ledger != null)
			{
				relay.Resync(ledger.UnlockedSteps);
			}
		}

		private void OnAchievementUnlocked(AchievementUnlockedEvent evt) => relay.OnAchievementUnlocked(evt.Step);
	}
}
