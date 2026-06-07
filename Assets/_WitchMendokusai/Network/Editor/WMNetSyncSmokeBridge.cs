using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace WitchMendokusai.NetworkEditor
{
	/// <summary>
	/// TASK-WM-187 — 호스트 PlayVerify 전용 minimal SyncVar bridge.
	///
	/// WorldClockNetworkBridge 의 SyncVar 채널 메커니즘과 *동일한* WMNetworkBehaviour/FishNet.SyncVar 파이프라인을
	/// 게임 DI 캐스케이드(WorldClock→TimeManager→GameEventManager → VContainer)에서 격리해 거동만 입증.
	/// 게임 heavy-boot wedge 회피 = host 하네스 실거동 가능 — 채널 메커니즘 first-use 박힘이 본 PR 의 검증 rung.
	///
	/// 실 WorldClock 시각 동기 거동 검증은 게임 heavy-boot 경로(별도) — 현 단계 = sync 1채널 substrate 박힘.
	///
	/// 검증 정본 = <see cref="ClientMirroredValue"/> — OnChange(asServer=false) 가 fire 한 시점의 신 값.
	/// 호스트 모드라도 server set 은 sync layer 큐 → 다음 tick 에 host client local connection 으로 적용 = 콜백 fire.
	/// </summary>
	public sealed class WMNetSyncSmokeBridge : WMNetworkBehaviour
	{
		public const int UNINITIALIZED_VALUE = -1;

		// SyncVar default ctor — initial value = default(T) = 0. Server set to PROBE_TEST_VALUE (≠0) → OnChange fire.
		private readonly SyncVar<int> probe = new SyncVar<int>();

		private int clientMirroredValue = UNINITIALIZED_VALUE;
		private bool clientFired;

		/// <summary> 클라(asServer=false) 측 OnChange 가 fire 했는가 — sync 레이어 라이브 신호. </summary>
		public bool ClientFired => clientFired;

		/// <summary> OnChange(asServer=false) 가 fire 했을 때 받은 새 값. </summary>
		public int ClientMirroredValue => clientMirroredValue;

		// 호스트 모드: OnStartServer + OnStartClient 양쪽 fire — 콜백 idempotent 등록 위해 isSubscribed 가드.
		// 정본 WorldClockNetworkBridge 패턴(OnStartServer override)과 정합.
		private bool isSubscribed;

		public override void OnStartServer()
		{
			base.OnStartServer();
			Subscribe();
		}

		public override void OnStartClient()
		{
			base.OnStartClient();
			Subscribe();
		}

		public override void OnStopServer()
		{
			base.OnStopServer();
			Unsubscribe();
		}

		public override void OnStopClient()
		{
			base.OnStopClient();
			Unsubscribe();
		}

		/// <summary> 서버에서 SyncVar 값 push. 클라(host client) 가 다음 tick 에 OnChange 콜백 fire. </summary>
		public void ServerSetProbe(int value)
		{
			probe.Value = value;
		}

		private void HandleProbeChanged(int previousValue, int nextValue, bool asServer)
		{
			if (asServer == true)
				return;

			clientFired = true;
			clientMirroredValue = nextValue;
		}

		private void Subscribe()
		{
			if (isSubscribed == true)
				return;
			probe.OnChange += HandleProbeChanged;
			isSubscribed = true;
		}

		private void Unsubscribe()
		{
			if (isSubscribed == false)
				return;
			probe.OnChange -= HandleProbeChanged;
			isSubscribed = false;
		}
	}
}
