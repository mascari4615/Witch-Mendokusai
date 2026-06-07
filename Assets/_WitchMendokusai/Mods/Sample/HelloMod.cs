using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.Mods.Sample
{
	/// <summary>
	/// TASK-WM-083 Phase A+B + TASK-WM-188 first-use.
	/// asmdef references = [WitchMendokusai.DomainSDK] 만 → DomainSDK type 만 호출 가능(sandbox 강제, WM-184 게이트).
	/// IMod.Initialize(IModContext) = 껍데기→실기능 seam: quest 1 + effect 1 실제 등록.
	/// </summary>
	public class HelloMod : IMod
	{
		public string Name => "Sample";
		public string Version => "0.1.0";
		public ModKind Kind => ModKind.Behavior;

		public void Initialize(IModContext context)
		{
			RuntimeQuestSaveData sampleQuest = new RuntimeQuestSaveData
			{
				Guid = Guid.NewGuid(),
				State = RuntimeQuestState.InProgress,
				SO_ID = -1,
				Name = "Hello Mod Quest",
				Description = "TASK-WM-188 first-use — DomainSDK POCO 만으로 quest 등록 sandbox 검증.",
				Type = QuestType.Normal,
				GameEvents = new List<GameEventType>(),
				Criteria = new List<RuntimeCriteriaSaveData>(),
				CompleteEffects = new List<EffectInfoData>(),
				RewardEffects = new List<RewardInfoData>(),
				Rewards = new List<RewardInfoData>(),
				WorkTime = 0f,
				AutoWork = false,
				AutoComplete = false,
			};
			context.ContentRegistry.RegisterQuest(sampleQuest);

			EffectInfoData sampleEffect = new EffectInfoData
			{
				Type = EffectType.UnitStat,
				DataSoID = WorkConstants.NONE_WORKER_ID,
				ArithmeticOperator = ArithmeticOperator.Add,
				Value = 0,
			};
			context.ContentRegistry.RegisterEffect(sampleEffect);

			Debug.Log($"[Mod:{Name} v{Version}] Initialize via IModContext — quest 1 + effect 1 registered ({Kind}).");
		}
	}
}
