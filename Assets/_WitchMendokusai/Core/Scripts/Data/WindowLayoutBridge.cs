using UnityEngine;

namespace WitchMendokusai
{
	public static class WindowLayoutBridge
	{
		private static WindowLayoutData data;

		public static void Register(WindowLayoutData windowLayoutData) => data = windowLayoutData;
		public static Vector2? GetPosition(string windowId) => data?.Get(windowId);
		public static void SetPosition(string windowId, Vector2 position) => data?.Set(windowId, position);
		public static bool? GetExpanded(string windowId) => data?.GetExpanded(windowId);
		public static void SetExpanded(string windowId, bool isExpanded) => data?.SetExpanded(windowId, isExpanded);
	}
}
