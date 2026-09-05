using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(WindowLayoutData), menuName = "WM/DataBuffer/" + nameof(WindowLayoutData))]
	public class WindowLayoutData : ScriptableObject, ISavable<List<WindowLayoutEntry>>
	{
		private readonly Dictionary<string, Vector2> positions = new();
		private readonly Dictionary<string, bool> expandedStates = new();

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

		public bool? GetExpanded(string windowId)
		{
			if (string.IsNullOrEmpty(windowId))
				return null;

			if (expandedStates.TryGetValue(windowId, out bool isExpanded))
				return isExpanded;

			return null;
		}

		public void SetExpanded(string windowId, bool isExpanded)
		{
			if (string.IsNullOrEmpty(windowId))
				return;

			expandedStates[windowId] = isExpanded;
		}

		public List<WindowLayoutEntry> Save()
		{
			HashSet<string> windowIds = new(positions.Keys);
			windowIds.UnionWith(expandedStates.Keys);

			List<WindowLayoutEntry> list = new(windowIds.Count);
			foreach (string id in windowIds)
			{
				positions.TryGetValue(id, out Vector2 position);
				expandedStates.TryGetValue(id, out bool isExpanded);
				list.Add(new WindowLayoutEntry { windowId = id, x = position.x, y = position.y, isExpanded = isExpanded });
			}
			return list;
		}

		public void Load(List<WindowLayoutEntry> saveData)
		{
			positions.Clear();
			expandedStates.Clear();
			if (saveData == null)
				return;

			foreach (WindowLayoutEntry entry in saveData)
			{
				positions[entry.windowId] = new Vector2(entry.x, entry.y);
				expandedStates[entry.windowId] = entry.isExpanded;
			}
		}
	}
}
