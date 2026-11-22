using System;
using System.Collections.Generic;
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

		[Tooltip("게임 업적 단계 (AchievementSO ID, 단계) 중 Steam 에 연결할 것만. 정본은 게임, 여기 없는 업적은 게임 전용.")]
		[SerializeField] private List<SteamAchievementLink> achievements = new();

		[Tooltip("게임 기록 키 (GameRecords) 중 Steam 순위표에 올릴 것만. 정본은 게임 세이브, 순위표는 사본.")]
		[SerializeField] private List<SteamLeaderboardLink> leaderboards = new();

		public uint AppId => appId;
		public bool InitializeInEditor => initializeInEditor;
		public IReadOnlyList<SteamAchievementLink> Achievements => achievements;
		public IReadOnlyList<SteamLeaderboardLink> Leaderboards => leaderboards;

		/// <summary>게임 업적 단계에서 Steam API 이름으로 가는 표. 같은 단계가 두 번이면 FastFail</summary>
		public IReadOnlyDictionary<AchievementStep, string> BuildAchievementMap()
		{
			Dictionary<AchievementStep, string> map = new();
			foreach (SteamAchievementLink link in achievements)
			{
				map.Add(link.Step, link.ApiName);
			}
			return map;
		}

		/// <summary>게임 기록 키에서 Steam 순위표 이름으로 가는 표. 같은 키가 두 번이면 FastFail</summary>
		public IReadOnlyDictionary<string, string> BuildLeaderboardMap()
		{
			Dictionary<string, string> map = new();
			foreach (SteamLeaderboardLink link in leaderboards)
			{
				map.Add(link.RecordKey, link.LeaderboardName);
			}
			return map;
		}

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
