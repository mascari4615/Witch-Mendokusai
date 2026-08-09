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

			try
			{
				RuntimeManager.MuteAllEvents(true);
				Debug.Log("[HeadlessAudioMute] 무음 실행 — FMOD 마스터 버스 mute (batchmode 또는 WM_MUTE=1).");
			}
			catch (Exception exception)
			{
				// 뱅크 미로드 등으로 FMOD 가 안 뜬 판은 이미 무음이다. 부팅을 막을 이유가 없다.
				Debug.LogWarning($"[HeadlessAudioMute] FMOD mute 실패 — FMOD 미초기화로 간주하고 진행. "
					+ $"{exception.GetType().Name}: {exception.Message}");
			}
		}
	}
}
