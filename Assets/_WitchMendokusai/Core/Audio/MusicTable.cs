using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 장면 상태마다 틀 FMOD 음악. 장면 이름으로 상태를 정하는 표도 같이 둠
	[CreateAssetMenu(fileName = nameof(MusicTable), menuName = "WM/MusicTable")]
	public class MusicTable : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			[SerializeField] private MusicState state;
			[SerializeField, Tooltip("FMOD 음악 이벤트 경로. 비우면 음악을 멈추고 조용히 둠 (승패 징글 뒤 등)")] private string eventPath = string.Empty;
			[SerializeField, Tooltip("곡이 끝나면 처음부터 다시")] private bool loop = true;

			public MusicState State => state;
			public string EventPath => eventPath;
			public bool Loop => loop;
		}

		[Serializable]
		public class SceneEntry
		{
			[SerializeField] private string sceneName = string.Empty;
			[SerializeField] private MusicState state;

			public string SceneName => sceneName;
			public MusicState State => state;
		}

		[SerializeField] private List<Entry> entries = new();
		[SerializeField, Tooltip("이 장면이 열리면 이 상태로. 표에 없는 장면 (로딩 등) 은 음악을 바꾸지 않음")] private List<SceneEntry> scenes = new();

		public IReadOnlyList<Entry> Entries => entries;

		public bool TryGetEntry(MusicState state, out Entry entry)
		{
			foreach (Entry each in entries)
			{
				if (each.State == state)
				{
					entry = each;
					return true;
				}
			}
			entry = null;
			return false;
		}

		public bool TryGetSceneState(string sceneName, out MusicState state)
		{
			foreach (SceneEntry each in scenes)
			{
				if (each.SceneName == sceneName)
				{
					state = each.State;
					return true;
				}
			}
			state = default;
			return false;
		}
	}
}
