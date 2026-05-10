namespace WitchMendokusai.Mods.Sample
{
	/// <summary>
	/// TASK-WM-083 Phase A — Mods SDK 첫 sandbox 검증.
	/// asmdef references = [WitchMendokusai.DomainSDK] 만 → DomainSDK type 만 호출 가능.
	/// Domain type (DataSO / SOHelper / QuestSO 등) 호출 시 컴파일 fail = sandbox 강제.
	/// 본 mod 의 *진정한 game-side 연결* (ModLoader / discovery / runtime 등록) 은 Phase B 자리.
	/// </summary>
	public static class HelloMod
	{
		public const string ModName = "Sample";
		public const string ModVersion = "0.1.0";

		// DomainSDK type reference 검증 (단방향 OK).
		public static QuestType DefaultQuestType => QuestType.Normal;
		public static EffectType DefaultEffectType => EffectType.UnitStat;
		public static int DefaultWorkerID => WorkConstants.NONE_WORKER_ID;
	}
}
