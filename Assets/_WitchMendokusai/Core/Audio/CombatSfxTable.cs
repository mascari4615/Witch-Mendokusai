using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(CombatSfxTable), menuName = "WM/CombatSfxTable")]
	public class CombatSfxTable : ScriptableObject
	{
		[Serializable]
		public class Entry
		{
			[SerializeField] private CombatSfxEvent sfxEvent;
			[SerializeField] private string eventPath = string.Empty;
			[SerializeField, Tooltip("같은 사건이 이 시간 안에 또 나면 거름 (초, 실시간)")] private float minIntervalSeconds = 0.05f;
			[SerializeField, Tooltip("켜면 호출한 위치에서 재생 (3D). 위치를 안 주면 위치 없이 재생")] private bool playAtPosition;
			[SerializeField, Min(0), Tooltip("같은 사건을 동시에 몇 개까지 낼지 (위치 재생일 때). 0 이면 세지 않고 최소 간격만")] private int maxVoices;

			public CombatSfxEvent SfxEvent => sfxEvent;
			public string EventPath => eventPath;
			public float MinIntervalSeconds => minIntervalSeconds;
			public bool PlayAtPosition => playAtPosition;
			public int MaxVoices => maxVoices;
		}

		[SerializeField] private List<Entry> entries = new();

		private Dictionary<CombatSfxEvent, Entry> lookup;

		public bool TryGetEntry(CombatSfxEvent sfxEvent, out Entry entry)
		{
			if (lookup == null)
			{
				lookup = new Dictionary<CombatSfxEvent, Entry>();
				foreach (Entry each in entries)
					lookup[each.SfxEvent] = each;
			}
			return lookup.TryGetValue(sfxEvent, out entry);
		}

		private void OnValidate() => lookup = null;
	}
}
