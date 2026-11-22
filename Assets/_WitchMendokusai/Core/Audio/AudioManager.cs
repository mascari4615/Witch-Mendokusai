using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class AudioManager : MonoBehaviour
	{
		public static AudioManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out AudioManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public enum BusType
		{
			Master = 0,
			BGM = 1,
			SFX = 2
		}

		private readonly Bus[] buses = new Bus[3];
		private EventInstance sfxVolumeTestEvent;
		private EventInstance bgmEvent;
		private EventInstance ambientEvent;
		private MusicTable musicTable;
		private MusicTable.Entry currentMusic;
		private MusicState musicState;
		private bool hasMusicState;
		private bool audioReady; // FMOD 뱅크 로드 성공 여부. false 면 모든 오디오 no-op(부팅·게임 계속, 무음).

		public bool AudioReady => audioReady;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			// 오디오는 부팅 필수의존 아님. FMOD 의 에디터 Play 뱅크 로딩은 *메인 에디터*에서만 돌아
			// MPPM 가상 플레이어엔 뱅크가 안 올라옴 → GetBus 가 throw → 예전엔 DI 컨테이너 빌드 abort
			// → 게임 전체 부팅 실패였음(WM-191 멀티 테스트서 발견). 뱅크 없으면 audioReady=false 로 무음
			// 진행(크래시 X — 뱅크 있는 메인 에디터/standalone 에선 audioReady=true → 동작 100% 동일=회귀 0).
			try
			{
				buses[(int)BusType.Master] = RuntimeManager.GetBus("bus:/");
				buses[(int)BusType.BGM] = RuntimeManager.GetBus("bus:/BGM");
				buses[(int)BusType.SFX] = RuntimeManager.GetBus("bus:/SFX");
				sfxVolumeTestEvent = RuntimeManager.CreateInstance("event:/SFX/SFXTest");
				audioReady = true;
			}
			catch (System.Exception exception)
			{
				audioReady = false;
				Debug.LogWarning($"[AudioManager] FMOD 뱅크 미로드 — 오디오 비활성(무음)으로 진행, 부팅 계속. "
					+ $"{exception.GetType().Name}: {exception.Message}");
			}
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
				SceneManager.sceneLoaded -= OnSceneLoaded;
			}
		}

		private void Start()
		{
			musicTable = Resources.Load<MusicTable>(nameof(MusicTable));
			if (musicTable == null)
				Debug.LogError($"{nameof(MusicTable)} not found");
			SceneManager.sceneLoaded += OnSceneLoaded;
			if (audioReady == false)
				return;
			UpdateVolume();
			OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
		}

		// 장면 표에 있는 장면이 열리면 그 장면의 음악으로. 로딩처럼 표에 없는 장면은 그대로 둠
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (musicTable != null && musicTable.TryGetSceneState(scene.name, out MusicState state))
				PlayMusic(state);
		}

		private void UpdateVolume()
		{
			if (audioReady == false)
				return;
			buses[(int)BusType.Master].setVolume(GetVolume(BusType.Master));
			buses[(int)BusType.BGM].setVolume(GetVolume(BusType.BGM));
			buses[(int)BusType.SFX].setVolume(GetVolume(BusType.SFX));
		}

		private Camera lastListenerCamera;

		/// <summary>
		/// 소리를 「듣는 귀」가 카메라에 붙어 있는지 확인하고, 없으면 붙인다.
		///
		/// ★ 귀가 없으면 방향·거리감이 통째로 사라진다 — 소리는 나는데 *어디서 나는지*가 없다.
		///   무대(씬)마다 사람이 손으로 붙이게 돼 있어서, 2026-08-07 실기 기준 **두 무대에만** 있었다.
		///   빠뜨렸다는 신호가 로그 경고 한 줄뿐이라 아무도 안 본다.
		/// ★ 왜 코드로 보장하나: 새 무대를 만들 때마다 다시 빠질 자리다. 무대 수가 늘수록 확실해지는
		///   쪽이 옳다. 이미 붙어 있으면 아무것도 안 한다(둘이 되면 FMOD 가 따로 경고한다).
		/// ★ 카메라는 무대가 바뀌면 갈린다 — 그래서 한 번이 아니라 바뀔 때마다 확인한다.
		/// </summary>
		private void EnsureStudioListener()
		{
			Camera camera = Camera.main;
			if (camera == null || camera == lastListenerCamera)
				return;

			lastListenerCamera = camera;
			if (camera.GetComponent<StudioListener>() != null
				|| camera.GetComponentInChildren<StudioListener>(true) != null)
			{
				return;
			}

			camera.gameObject.AddComponent<StudioListener>();
			Debug.Log("[AudioManager] 카메라에 소리 듣는 귀가 없어 붙였다 — 3D 거리감이 살아난다.");
		}

		private void Update()
		{
			EnsureStudioListener();

			if (audioReady == false || currentMusic == null)
				return;

			// TODO : else if (DataManager.Instance.CurGameData.muteOnOutfocus)
			{
				// TODO : master.setVolume(0);
			}

			bgmEvent.getPlaybackState(out PLAYBACK_STATE playbackState);
			bool hasEventPath = string.IsNullOrEmpty(currentMusic.EventPath) == false;
			if (MusicFlow.ShouldRestart(currentMusic.Loop, hasEventPath, playbackState == PLAYBACK_STATE.STOPPED))
				StartMusicEvent(currentMusic.EventPath);
		}

		public void StopMusic()
		{
			if (audioReady == false)
				return;
			currentMusic = null;
			hasMusicState = false;
			bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			bgmEvent.release();
		}

		// 장면 상태의 음악으로. 같은 상태를 다시 요청하면 곡을 처음부터 다시 틀지 않음
		public void PlayMusic(MusicState state)
		{
			if (audioReady == false || musicTable == null)
				return;
			if (MusicFlow.ShouldSwitch(hasMusicState, musicState, state) == false)
				return;
			if (musicTable.TryGetEntry(state, out MusicTable.Entry entry) == false)
			{
				Debug.LogWarning($"[AudioManager] {nameof(MusicTable)} 에 {state} 항목이 없음, 음악 유지");
				return;
			}
			string previousPath = currentMusic != null ? currentMusic.EventPath : null;
			musicState = state;
			hasMusicState = true;
			currentMusic = entry;
			// 같은 곡이면 이어서 틀기. 멈춰 있었으면 Update 의 반복이 다시 틀어 줌
			if (MusicFlow.IsSameTrack(previousPath, entry.EventPath))
				return;
			StartMusicEvent(entry.EventPath);
		}

		public static void RequestMusic(MusicState state)
		{
			if (TryGetExistingInstance(out AudioManager audioManager))
				audioManager.PlayMusic(state);
		}

		public void PlayAmbient(string eventPath)
		{
			if (audioReady == false)
				return;
			ambientEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			ambientEvent.release();
			if (string.IsNullOrEmpty(eventPath) == false)
			{
				try
				{
					ambientEvent = RuntimeManager.CreateInstance(eventPath);
					ambientEvent.start();
				}
				catch (EventNotFoundException)
				{
					// FMOD 뱅크에 등록 안 된 event — silent skip (warning 로그). WeatherSO SfxKey
					// (e.g. weather_clear) 가 실제 FMOD event 명과 mismatch 일 때 Play 중단 방지.
					Debug.LogWarning($"[AudioManager] FMOD event not found: '{eventPath}' — ambient skip");
				}
			}
		}

		public void StopAmbient()
		{
			if (audioReady == false)
				return;
			ambientEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			ambientEvent.release();
		}

		public void PlaySfx(string eventPath)
		{
			if (audioReady == false)
				return;
			if (string.IsNullOrEmpty(eventPath))
				return;
			try
			{
				RuntimeManager.PlayOneShot(eventPath);
			}
			catch (EventNotFoundException)
			{
				// 뱅크에 없는 데이터 키 (WeatherSO ThunderSfxKey 등): 호출 코루틴 중단 방지
				Debug.LogWarning($"[AudioManager] FMOD event not found: '{eventPath}', sfx skip");
			}
		}

		// 앞 곡은 페이드로 내보내고 새 곡 시작. 경로가 비면 조용히 둠
		private void StartMusicEvent(string eventPath)
		{
			bgmEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			bgmEvent.release();
			if (string.IsNullOrEmpty(eventPath))
				return;
			try
			{
				bgmEvent = RuntimeManager.CreateInstance(eventPath);
				bgmEvent.start();
			}
			catch (EventNotFoundException)
			{
				Debug.LogWarning($"[AudioManager] FMOD event not found: '{eventPath}', music skip");
				currentMusic = null;
				// 같은 상태를 다시 요청하면 다시 시도하게 (뱅크가 늦게 올라오는 경우)
				hasMusicState = false;
			}
		}

		public float GetVolume(BusType busType)
		{
			string key = $"Volume{(int)busType}";

			if (PlayerPrefs.HasKey(key) == false)
				PlayerPrefs.SetFloat(key, 1);

			float volume = PlayerPrefs.GetFloat(key);
			return volume;
		}

		public void SetVolume(BusType busType, float volume)
		{
			string key = $"Volume{(int)busType}";
			PlayerPrefs.SetFloat(key, volume);
			if (audioReady == false)
				return;
			buses[(int)busType].setVolume(volume);

			if (busType == BusType.SFX)
			{
				sfxVolumeTestEvent.getPlaybackState(out PLAYBACK_STATE playbackState);
				if (playbackState != PLAYBACK_STATE.PLAYING)
					sfxVolumeTestEvent.start();
			}
		}
	}
}
