using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	// 창 포커스를 잃으면 게임 시간과 소리를 멈추고, 돌아오면 이어 간다 (데모 조사 2026-11-23 막는 것 9)
	// runInBackground 는 켠 채 둔다. 부팅 스모크와 성능 시나리오가 포커스 없는 창으로 돌기 때문
	// 시간은 TimeManager 의 정지 소유자 하나로 들어감. ESC 메뉴 정지와 겹쳐도 돌아올 때 자기 정지만 풂
	// 소리는 음소거가 아니라 마스터 버스 일시정지. 음악이 끊긴 자리부터 이어짐
	public class FocusPause : MonoBehaviour
	{
		private bool isHolding;

		// 사람이 안 보는 실행은 멈추지 않음
		// batchmode: 2-peer, solo 스모크. 결정 부팅: 부팅 스모크, 성능 시나리오. WM_MUTE: 창 띄운 무음 실행
		// 에디터: 에이전트가 포커스 없는 에디터에서 Play, 캡처, 성장 봇을 돌림. Play 진입 도메인 리로드로 정적 표시도 못 씀
		public static bool IsExempt(bool isBatchMode, bool isDeterministic, bool isSilentRun, bool isEditor)
			=> isBatchMode || isDeterministic || isSilentRun || isEditor;

		public static bool IsExemptNow
			=> IsExempt(Application.isBatchMode, BootMode.IsDeterministic, HeadlessAudioMute.IsSilentRun, Application.isEditor);

		public static bool ShouldPause(bool hasFocus, bool exempt) => hasFocus == false && exempt == false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			if (IsExemptNow)
				return;
			GameObject host = new(nameof(FocusPause));
			DontDestroyOnLoad(host);
			host.AddComponent<FocusPause>();
		}

		// 뒤에서 켜진 창은 첫 포커스 알림을 못 받을 수 있음
		private void Start() => OnApplicationFocus(Application.isFocused);

		// 포커스를 잃을 때 TimeManager 가 아직 없었거나 새로 생겼으면 여기서 소유자로 들어감 (Pause 는 중복 등록 무시)
		private void Update()
		{
			if (isHolding && TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.Pause(gameObject);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (ShouldPause(hasFocus, IsExemptNow))
				Hold();
			else if (hasFocus)
				Release();
		}

		private void OnDestroy() => Release();

		private void Hold()
		{
			if (isHolding)
				return;
			isHolding = true;
			Update();
			SetAudioPaused(true);
		}

		private void Release()
		{
			if (isHolding == false)
				return;
			isHolding = false;
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.Resume(gameObject);
			SetAudioPaused(false);
		}

		private static void SetAudioPaused(bool paused)
		{
			// FMOD 가 아직 안 떴으면 낼 소리도 없음. 여기서 Instance 를 건드리면 FMOD 를 억지로 띄움
			if (RuntimeManager.IsInitialized == false)
				return;
			RuntimeManager.PauseAllEvents(paused);
		}
	}
}
