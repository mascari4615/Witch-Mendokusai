using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class MusicTableTest
	{
		private const string MUSIC_PREFIX = "event:/BGM/";
		private static readonly Regex OBJECT_NAME = new("<object class=\"(Event|EventFolder)\" id=\"(\\{[^}]+\\})\">\\s*<property name=\"name\">\\s*<value>([^<]*)</value>");
		private static readonly Regex PARENT_FOLDER = new("<relationship name=\"folder\">\\s*<destination>(\\{[^}]+\\})</destination>");

		private static MusicTable LoadTable()
		{
			MusicTable table = Resources.Load<MusicTable>(nameof(MusicTable));
			Assert.IsNotNull(table, nameof(MusicTable));
			return table;
		}

		// FMOD 프로젝트 메타데이터에서 이벤트 전체 경로 (event:/폴더/이름). 뱅크를 열지 않고 경로 오타와 지워진 곡을 잡음
		// 이름만 보면 SFX 폴더의 같은 이름 (Victory 등) 이 통과하므로 폴더 사슬까지 풀어 비교
		private static HashSet<string> FmodEventPaths()
		{
			string metadata = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FMODAssets", "Metadata"));
			Dictionary<string, (string name, string parent)> folders = new();
			List<(string name, string parent)> events = new();
			foreach (string kind in new[] { "EventFolder", "Event" })
			{
				string folder = Path.Combine(metadata, kind);
				Assert.IsTrue(Directory.Exists(folder), folder);
				foreach (string file in Directory.GetFiles(folder, "*.xml"))
				{
					string text = File.ReadAllText(file);
					Match name = OBJECT_NAME.Match(text);
					if (name.Success == false)
						continue;
					Match parent = PARENT_FOLDER.Match(text);
					(string, string) item = (name.Groups[3].Value, parent.Success ? parent.Groups[1].Value : string.Empty);
					if (name.Groups[1].Value == "EventFolder")
						folders[name.Groups[2].Value] = item;
					else
						events.Add(item);
				}
			}

			HashSet<string> paths = new();
			foreach ((string name, string parent) in events)
			{
				string path = name;
				string folderId = parent;
				// 최상위 폴더 (Master) 는 EventFolder 파일에 없어 사슬이 거기서 끝남
				while (folders.TryGetValue(folderId, out (string name, string parent) folder))
				{
					path = folder.Item1 + "/" + path;
					folderId = folder.Item2;
				}
				paths.Add("event:/" + path);
			}
			return paths;
		}

		[Test]
		public void EveryStateHasEntry()
		{
			MusicTable table = LoadTable();
			foreach (MusicState state in Enum.GetValues(typeof(MusicState)))
				Assert.IsTrue(table.TryGetEntry(state, out MusicTable.Entry _), state.ToString());
		}

		[Test]
		public void EveryMusicPathExistsInFmodProject()
		{
			HashSet<string> paths = FmodEventPaths();
			Assert.IsTrue(paths.Contains("event:/SFX/Combat/Victory"), "경로 풀기 자체 확인");
			foreach (MusicTable.Entry entry in LoadTable().Entries)
			{
				if (string.IsNullOrEmpty(entry.EventPath))
					continue;
				StringAssert.StartsWith(MUSIC_PREFIX, entry.EventPath, entry.State.ToString());
				Assert.IsTrue(paths.Contains(entry.EventPath), entry.State + ": " + entry.EventPath);
			}
		}

		[TestCase("Intro", MusicState.Title)]
		[TestCase("Lobby", MusicState.Lobby)]
		[TestCase("World", MusicState.World)]
		public void MainScenesPickMusic(string sceneName, MusicState expected)
		{
			Assert.IsTrue(LoadTable().TryGetSceneState(sceneName, out MusicState state), sceneName);
			Assert.AreEqual(expected, state);
		}

		[Test]
		public void LoadingSceneKeepsCurrentMusic()
			=> Assert.IsFalse(LoadTable().TryGetSceneState("Loading", out MusicState _));

		[TestCase(false, MusicState.World, MusicState.World, true)]
		[TestCase(true, MusicState.World, MusicState.World, false)]
		[TestCase(true, MusicState.Dungeon, MusicState.Boss, true)]
		[TestCase(true, MusicState.Victory, MusicState.Dungeon, false)]
		[TestCase(true, MusicState.Defeat, MusicState.Boss, false)]
		[TestCase(true, MusicState.Victory, MusicState.World, true)]
		[TestCase(true, MusicState.Defeat, MusicState.Victory, true)]
		public void SwitchOnlyOnNewState(bool hasCurrent, MusicState current, MusicState requested, bool expected)
			=> Assert.AreEqual(expected, MusicFlow.ShouldSwitch(hasCurrent, current, requested));

		[TestCase(true, true, true, true)]
		[TestCase(true, true, false, false)]
		[TestCase(false, true, true, false)]
		[TestCase(true, false, true, false)]
		public void RestartOnlyLoopingMusicThatStopped(bool loop, bool hasEventPath, bool stopped, bool expected)
			=> Assert.AreEqual(expected, MusicFlow.ShouldRestart(loop, hasEventPath, stopped));

		[TestCase("event:/BGM/A", "event:/BGM/A", true)]
		[TestCase("event:/BGM/A", "event:/BGM/B", false)]
		[TestCase(null, "event:/BGM/A", false)]
		[TestCase("", "", false)]
		public void SameTrackContinues(string currentPath, string nextPath, bool expected)
			=> Assert.AreEqual(expected, MusicFlow.IsSameTrack(currentPath, nextPath));
	}
}
