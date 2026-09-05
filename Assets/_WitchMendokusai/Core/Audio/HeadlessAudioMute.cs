using System;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 사람이 안 보는 실행에서는 소리를 내지 않는다.
	///
	/// ★ 왜 필요한가 (2026-08-09): 노트북은 빌드머신이면서 사용자의 *알람 기기*다. 빌드가 끝나면
	///   런타임 게이트(부팅 스모크 · world 2-peer)가 진짜 플레이어를 3~4번 띄우는데, 그 판들이
	///   BGM 을 랜덤 재생한다 — 사람이 없는 검사가 방 안에 소리를 낸다. 시스템 볼륨을 내리면
	///   알람까지 같이 죽으므로, 끄는 자리는 *게임 쪽*이어야 한다.
	/// ★ 자리 선택: 게이트 스크립트마다 env 를 챙기게 하면 새 게이트가 생길 때마다 또 빠진다.
	///   `-batchmode` 로 뜬 플레이어는 정의상 사람이 조작하지 않으므로 **그 자체가 무음 신호**다.
	///   env `WM_MUTE=1` 은 창을 띄운 채로 조용히 돌리고 싶을 때의 수동 스위치.
	/// ★ 무음 = 마스터 버스 mute. 볼륨을 0 으로 덮어쓰면 AudioManager 가 PlayerPrefs 로 되돌리지만
	///   mute 는 RuntimeManager 가 상태로 들고 재적용하므로 나중 볼륨 변경에 안 풀린다.
	/// </summary>
	public static class HeadlessAudioMute
	{
		private const string MUTE_ENV = "WM_MUTE";

		/// <summary>
		/// 부팅 스모크가 로그에서 찾는 표식. **이 줄이 없으면 게이트가 빨개진다.**
		///
		/// ★ 왜 표식이 필요한가: 무음은 조용히 깨진다 — 초기화 순서가 바뀌어 mute 가 안 먹어도
		///   화면·결과 파일은 그대로 통과한다. 그러면 발견 경로가 「사용자가 새벽에 깜짝 놀람」
		///   하나뿐이다. 알람 기기 옆에서 그건 못 쓸 방어다.
		/// ★ ASCII 인 이유: PS 5.1/cp949 로 로그를 읽는 자리가 있어 한글 표식은 깨져서 못 찾는다.
		/// </summary>
		public const string SILENT_MARKER = "[HeadlessAudioMute] SILENT-RUN-OK";

		/// <summary>사람 없는 실행인가 — 무음으로 돌아야 하는가.</summary>
		public static bool IsSilentRun
			=> Application.isBatchMode || Environment.GetEnvironmentVariable(MUTE_ENV) == "1";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Apply()
		{
			if (IsSilentRun == false)
				return;

			// FMOD 밖의 소리(비디오·AudioSource)까지 덮는 한 줄. 현재 프로젝트엔 없지만 새로 붙어도 샌다.
			AudioListener.volume = 0f;

			string how;
			try
			{
				RuntimeManager.MuteAllEvents(true);
				// 「불렀다」가 아니라 「먹었다」를 확인한다 — 부른 것만으로 초록을 주면 표식이 거짓말을 한다.
				how = RuntimeManager.IsMuted ? "fmod-bus-muted" : string.Empty;
			}
			catch (Exception exception)
			{
				// FMOD 자체가 안 뜬 판(뱅크 미로드 등)은 애초에 소리가 안 난다 = 이것도 진짜 무음이다.
				how = IsFmodDown() ? "fmod-not-initialized" : string.Empty;
				Debug.LogWarning($"[HeadlessAudioMute] FMOD mute 호출 실패 — {exception.GetType().Name}: {exception.Message}");
			}

			if (string.IsNullOrEmpty(how))
			{
				// 여기서 멈추지 않는다(검사 자체는 계속 돌아야 한다). 대신 스모크가 이 판을 빨갛게 만든다.
				Debug.LogError("[HeadlessAudioMute] SILENT-RUN-FAILED — 무음 보장 실패. "
					+ "사람 없는 실행인데 소리가 날 수 있다 (노트북이 알람 기기다).");
				return;
			}

			Debug.Log($"{SILENT_MARKER} ({how}, listener-volume=0)");
		}

		/// <summary>FMOD 가 아예 안 떠 있나 — 확인 자체가 터지면 「모른다」이므로 무음으로 안 친다.</summary>
		private static bool IsFmodDown()
		{
			try
			{
				return RuntimeManager.IsInitialized == false;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
