using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(UiSfxTable), menuName = "WM/UiSfxTable")]
	public class UiSfxTable : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			[SerializeField] private UiSfxEvent sfxEvent;
			[SerializeField] private string eventPath = string.Empty;
			[SerializeField] private float minIntervalSeconds = 0.05f;
			[SerializeField, TextArea] private string candidates = string.Empty;

			public UiSfxEvent SfxEvent => sfxEvent;
			public string EventPath => eventPath;
			public float MinIntervalSeconds => minIntervalSeconds;
			public string Candidates => candidates;
		}

		[SerializeField] private List<Entry> entries = new();

		private Dictionary<UiSfxEvent, Entry> lookup;

		public bool TryGetEntry(UiSfxEvent sfxEvent, out Entry entry)
		{
			if (lookup == null)
			{
				lookup = new Dictionary<UiSfxEvent, Entry>();
				foreach (Entry each in entries)
					lookup[each.SfxEvent] = each;
			}
			return lookup.TryGetValue(sfxEvent, out entry);
		}

		private void OnValidate() => lookup = null;
	}
}
