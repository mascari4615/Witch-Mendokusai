using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(WindowLayoutData), menuName = "WM/DataBuffer/" + nameof(WindowLayoutData))]
	public class WindowLayoutData : ScriptableObject, ISavable<List<WindowLayoutEntry>>
	{
		private readonly Dictionary<string, Vector2> positions = new();

		public Vector2? Get(string windowId)
		{
			if (string.IsNullOrEmpty(windowId))
				return null;

			if (positions.TryGetValue(windowId, out Vector2 position))
				return position;

			return null;
		}

		public void Set(string windowId, Vector2 position)
		{
			if (string.IsNullOrEmpty(windowId))
				return;

			positions[windowId] = position;
		}

		public List<WindowLayoutEntry> Save()
		{
			List<WindowLayoutEntry> list = new(positions.Count);
			foreach ((string id, Vector2 position) in positions)
				list.Add(new WindowLayoutEntry { windowId = id, x = position.x, y = position.y });
			return list;
		}

		public void Load(List<WindowLayoutEntry> saveData)
		{
			positions.Clear();
			if (saveData == null)
				return;

			foreach (WindowLayoutEntry entry in saveData)
				positions[entry.windowId] = new Vector2(entry.x, entry.y);
		}
	}
}
