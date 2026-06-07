using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-188 — IModContentRegistry 의 Core 어댑터.
	/// ctor 가 ModContentRegistryBridge.Register(this) → DomainSDK 정적 등록 정본 + ModLoader.InitializeDiscoveredMods 트리거(껍데기→실기능 seam).
	/// QuestAddedEvent 구독자 0(grep 확인) → AddQuest 가 boot 시 안전. RuntimeQuest 는 saveData + 빈 Criteria 로 직접 구성(factory StartQuest 회피 = GameEventBridge 등록 0).
	/// Effect 는 in-process catalog 에 저장(즉시 apply X — boot 시 PlayerProvider 등 미준비 deps 회피, 후속 quest definition 참조용 seam).
	/// </summary>
	public class ModContentRegistryHost : IModContentRegistry
	{
		private readonly QuestManager questManager;
		private readonly List<EffectInfoData> registeredModEffects = new();

		public IReadOnlyList<EffectInfoData> RegisteredModEffects => registeredModEffects;

		[Inject]
		public ModContentRegistryHost(QuestManager questManager)
		{
			this.questManager = questManager;
			ModContentRegistryBridge.Register(this);
			ModLoader.InitializeDiscoveredMods(new ModContext(this));
		}

		public void RegisterQuest(RuntimeQuestSaveData questSaveData)
		{
			RuntimeQuest quest = new RuntimeQuest(questSaveData) { Criteria = new List<RuntimeCriteria>() };
			questManager.AddQuest(quest);
			Debug.Log($"[ModContentRegistryHost] RegisterQuest — {questSaveData.Name} (Guid={questSaveData.Guid}, Type={questSaveData.Type})");
		}

		public void RegisterEffect(EffectInfoData effectInfoData)
		{
			registeredModEffects.Add(effectInfoData);
			Debug.Log($"[ModContentRegistryHost] RegisterEffect — Type={effectInfoData.Type}, DataSoID={effectInfoData.DataSoID}, Value={effectInfoData.Value}");
		}
	}
}
