namespace WitchMendokusai
{
	public enum ContentCameraMode
	{
		None = -1,

		Normal = 0,
		Dungeon = 1,

		// SimCity Phase 1 (TASK-WM-164): 도시 부감 시점. append-only (Arena 는 다음 값 — session-bus 합의).
		CityView = 2,
	}

	public enum UICameraMode
	{
		None = -1,

		NPC = 0,

		Tab = 20,
	}
}
