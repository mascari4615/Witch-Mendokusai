namespace WitchMendokusai
{
	/// <summary>
	/// KeybindRegistry 가 노출하는 단일 엔트리. UI 안내·충돌 검증·리바인드의 기본 단위.
	/// </summary>
	public readonly struct KeybindEntry
	{
		public InputEventType EventType { get; }
		public string Category { get; }
		public string DisplayName { get; }
		public string DefaultPath { get; }
		public string CurrentPath { get; }

		public KeybindEntry(InputEventType eventType, string category, string displayName, string defaultPath, string currentPath)
		{
			EventType = eventType;
			Category = category;
			DisplayName = displayName;
			DefaultPath = defaultPath;
			CurrentPath = currentPath;
		}
	}
}
