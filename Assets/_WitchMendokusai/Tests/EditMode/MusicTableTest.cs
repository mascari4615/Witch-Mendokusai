using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class MusicTableTest
	{
		private const string MUSIC_PREFIX = "event:/BGM/";

		private static MusicTable LoadTable()
		{
			MusicTable table = Resources.Load<MusicTable>(nameof(MusicTable));
			Assert.IsNotNull(table, nameof(MusicTable));
			return table;
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
			HashSet<string> paths = FmodProjectPaths.Load();
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
