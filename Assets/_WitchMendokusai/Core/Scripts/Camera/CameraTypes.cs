namespace WitchMendokusai
{
	public enum ContentCameraMode
	{
		None = -1,

		Normal = 0,
		Dungeon = 1,

		// SimCity Phase 1 (TASK-WM-164): 도시 부감 시점. append-only (Arena 는 다음 값 — session-bus 합의).
		CityView = 2,

		// 마계 투기장 (TASK-WM-165): 아레나 관전(자유 궤도 orbit + 선수 포커스). append-only.
		Arena = 3,
	}

	public enum UICameraMode
	{
		None = -1,

		NPC = 0,

		Tab = 20,
	}
}
