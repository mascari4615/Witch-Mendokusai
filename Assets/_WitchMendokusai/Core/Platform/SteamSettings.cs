using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Steam 연동 설정의 정본 (App ID 한 곳).
	/// 런타임 SteamService 와 빌드 후처리 (steam_appid.txt 쓰기) 가 같은 값 사용.
	/// 자리가 Core 인 이유: 모바일 빌드에도 Resources 로 실리므로 스크립트 누락 방지.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(SteamSettings), menuName = "WM/Platform/Steam Settings")]
	public sealed class SteamSettings : ScriptableObject
	{
		// 자산 이름 규격 {타입}_{ID}_{이름} (CREDIT_0_Fonts 선례)
		public const string RESOURCE_NAME = "SETTINGS_0_Steam";

		[Tooltip("Steamworks 앱 ID. 480 은 Valve 시험용 (Spacewar).")]
		[SerializeField] private uint appId;

		[Tooltip("에디터 Play 에서도 Steam 에 붙을지. 끄면 빌드에서만 연결.")]
		[SerializeField] private bool initializeInEditor;

		public uint AppId => appId;
		public bool InitializeInEditor => initializeInEditor;

		public static SteamSettings Load()
		{
			SteamSettings settings = Resources.Load<SteamSettings>(RESOURCE_NAME);
			if (settings == null)
			{
				throw new InvalidOperationException(
					"[SteamSettings] Resources/" + RESOURCE_NAME + ".asset 없음. Core/Resources 배치 확인");
			}
			return settings;
		}
	}
}
