namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-188 — 모드가 콘텐츠(quest/effect) 를 게임에 등록하는 sandbox API.
	/// DomainSDK POCO 만 받음(QuestSO/EffectInfo Domain 타입 X) — asmdef 단방향 + WM-184 게이트가 sandbox 강제.
	/// 구현 = Core ModContentRegistryHost (ModContentRegistryBridge.Register 로 주입).
	/// </summary>
	public interface IModContentRegistry
	{
		void RegisterQuest(RuntimeQuestSaveData questSaveData);
		void RegisterEffect(EffectInfoData effectInfoData);
	}
}
