using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// Steam 설정 정본 잠금. App ID 는 SteamSettings 한 곳, steam_appid.txt 는 빌드 때 그 값으로 생성.
	/// </summary>
	public sealed class SteamSettingsTest
	{
		private const uint SPACEWAR_TEST_APP_ID = 480;
		private const string STEAM_ASMDEF_PATH = "Assets/_WitchMendokusai/Platform/Steam/WM.Platform.Steam.asmdef";

		[Test]
		public void Settings_load_with_real_app_id()
		{
			SteamSettings settings = SteamSettings.Load();

			Assert.That(settings.AppId, Is.Not.EqualTo(0u), "App ID 비어 있음");
			Assert.That(settings.AppId, Is.Not.EqualTo(SPACEWAR_TEST_APP_ID), "시험용 Spacewar ID 그대로");
		}

		[Test]
		public void Steam_assembly_is_desktop_only()
		{
			string json = File.ReadAllText(STEAM_ASMDEF_PATH);
			AsmdefPlatforms asmdef = JsonUtility.FromJson<AsmdefPlatforms>(json);

			Assert.That(asmdef.includePlatforms, Is.EquivalentTo(new[] { "Editor", "WindowsStandalone64" }),
				"Facepunch.Steamworks 는 Win64 플러그인만 켜져 있음. 다른 플랫폼에 열면 dll 없이 빌드 실패");
		}

		[Test]
		public void Achievement_links_point_to_game_achievements()
		{
			SteamSettings settings = SteamSettings.Load();
			Dictionary<int, QuestSO> questsById = new();
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(QuestSO)))
			{
				QuestSO quest = AssetDatabase.LoadAssetAtPath<QuestSO>(AssetDatabase.GUIDToAssetPath(guid));
				if (questsById.ContainsKey(quest.ID))
				{
					// 같은 ID 의 QuestSO 가 둘이면 어느 쪽에 연결됐는지 모호. 이 검사의 관심사는 연결된 ID 만
					continue;
				}
				questsById.Add(quest.ID, quest);
			}

			HashSet<string> apiNames = new();
			foreach (SteamAchievementLink link in settings.Achievements)
			{
				Assert.That(questsById.ContainsKey(link.QuestId), Is.True, "연결된 게임 업적 없음: " + link.QuestId);
				Assert.That(questsById[link.QuestId].Data.Type, Is.EqualTo(QuestType.Achievement),
					"Achievement 타입이 아닌 퀘스트를 Steam 업적에 연결: " + link.QuestId);
				Assert.That(Regex.IsMatch(link.ApiName ?? string.Empty, "^[A-Z0-9_]+$"), Is.True, "API 이름 형식: " + link.ApiName);
				Assert.That(apiNames.Add(link.ApiName), Is.True, "API 이름 중복: " + link.ApiName);
			}
			Assert.DoesNotThrow(() => settings.BuildAchievementMap(), "같은 게임 업적을 두 번 연결");
		}

		[Test]
		public void Leaderboard_links_point_to_game_records()
		{
			SteamSettings settings = SteamSettings.Load();
			HashSet<int> dungeonIds = new();
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(Dungeon)))
			{
				dungeonIds.Add(AssetDatabase.LoadAssetAtPath<Dungeon>(AssetDatabase.GUIDToAssetPath(guid)).ID);
			}

			HashSet<string> names = new();
			foreach (SteamLeaderboardLink link in settings.Leaderboards)
			{
				Match match = Regex.Match(link.RecordKey ?? string.Empty, @"^Dungeon\.(\d+)\.(\w+)$");
				Assert.That(match.Success, Is.True, "기록 키 형식: " + link.RecordKey);
				int dungeonId = int.Parse(match.Groups[1].Value);
				Assert.That(dungeonIds.Contains(dungeonId), Is.True, "없는 던전: " + link.RecordKey);
				Assert.That(System.Enum.TryParse(match.Groups[2].Value, out DungeonRecordStat stat), Is.True, "없는 기록 항목: " + link.RecordKey);
				Assert.That(link.RecordKey, Is.EqualTo(DungeonRecordRules.RecordKey(dungeonId, stat)));
				Assert.That(link.LowerIsBetter, Is.EqualTo(stat == DungeonRecordStat.FastestClear), "정렬 방향: " + link.RecordKey);
				Assert.That(Regex.IsMatch(link.LeaderboardName ?? string.Empty, "^[A-Za-z0-9_]+$"), Is.True, "순위표 이름 형식: " + link.LeaderboardName);
				Assert.That(names.Add(link.LeaderboardName), Is.True, "순위표 이름 중복: " + link.LeaderboardName);
			}
			Assert.DoesNotThrow(() => settings.BuildLeaderboardMap(), "같은 기록 키를 두 번 연결");
		}

		[System.Serializable]
		private sealed class AsmdefPlatforms
		{
			public string[] includePlatforms = new string[0];
		}
	}
}
