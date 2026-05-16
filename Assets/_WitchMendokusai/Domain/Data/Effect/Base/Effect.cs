using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public interface IEffect
	{
		void Apply(EffectInfo effectInfo);
	}

	// TASK-WM-107 Slice 2A — DI-managed dispatch seam (자기소멸 transitional).
	// EffectRunner 가 ctx 주입 가능하면 이 경로로, 정적 SO 호출처는 IEffect.Apply 구 경로.
	// 모든 Effect 가 IContextualEffect 로 수렴하면 IEffect.Apply(EffectInfo) 폐기 + 단일화.
	public interface IContextualEffect : IEffect
	{
		void Apply(EffectInfo effectInfo, EffectContext context);
	}

	public class Effect
	{
		// EffectInfoData → EffectInfo 해석 (dataSO lookup). 정적 경로 + EffectRunner 공용 (분기 무발산).
		// VQuests = SOManagerBridge 잔존 (transitional — Slice 2 후속 ctx 흡수 대상).
		public static EffectInfo ResolveEffectInfo(EffectInfoData data)
		{
			Debug.Log(data.Type + " " + data.DataSoID + " " + data.ArithmeticOperator + " " + data.Value);

			int id = data.DataSoID;
			EffectType effectType = data.Type;
			DataSO dataSO = null;

			switch (data.Type)
			{
				case EffectType.AddCard:
					dataSO = GetCardData(id);
					break;
				case EffectType.AddQuest:
					dataSO = GetQuestSO(id);
					break;
				case EffectType.AddRandomVillageQuest:
					effectType = EffectType.AddQuest;
					dataSO = SOManagerBridge.VQuests.Data[Random.Range(0, SOManagerBridge.VQuests.Data.Count)];
					break;
				case EffectType.FloatVariable:
					break;
				case EffectType.IntVariable:
					break;
				case EffectType.Item:
					dataSO = GetItemData(id);
					break;
				case EffectType.SpawnObject:
					break;
				case EffectType.UnitStat:
					dataSO = GetUnitStatData(id);
					break;
				case EffectType.GameStat:
					dataSO = GetGameStatData(id);
					break;
				case EffectType.UnlockQuest:
					dataSO = GetQuestSO(id);
					break;
				case EffectType.UnlockRecipe:
					dataSO = GetItemData(id);
					break;
				case EffectType.DungeonStat:
					dataSO = GetDungeonStatData(id);
					break;
				default:
					break;
			}

			return new EffectInfo()
			{
				Type = effectType,
				Data = dataSO,
				ArithmeticOperator = data.ArithmeticOperator,
				Value = data.Value
			};
		}

		// EffectType → IEffect 인스턴스. 정적 경로 + EffectRunner 공용.
		public static IEffect CreateEffect(EffectType effectType)
		{
			switch (effectType)
			{
				case EffectType.AddCard:
					return new AddCardEffect();
				case EffectType.AddQuest:
				case EffectType.AddRandomVillageQuest:
					return new AddQuestEffect();
				case EffectType.FloatVariable:
					return new FloatVariableEffect();
				case EffectType.IntVariable:
					return new IntVariableEffect();
				case EffectType.Item:
					return new ItemEffect();
				case EffectType.SpawnObject:
					return new SpawnObjectEffect();
				case EffectType.UnitStat:
					return new StatEffect();
				case EffectType.GameStat:
					return new GameStatEffect();
				case EffectType.UnlockQuest:
					return new UnlockQuestEffect();
				case EffectType.UnlockRecipe:
					return new UnlockRecipeEffect();
				case EffectType.DungeonStat:
					return new DungeonStatEffect();
				case EffectType.PlayDialogue:
					return new PlayDialogueEffect();
				case EffectType.PlayFade:
					return new PlayFadeEffect();
				default:
					return null;
			}
		}

		// 정적 경로 (CardData/UpgradeData 등 SO POCO 호출처 — ctx 없음, transitional).
		// DI-managed 호출처는 IEffectRunner 경유 (ctx 주입).
		public static void ApplyEffects(List<EffectInfoData> effectInfoData)
		{
			Debug.Log("Applying effects...");

			foreach (EffectInfoData data in effectInfoData)
				ApplyEffect(ResolveEffectInfo(data));
		}

		public static void ApplyEffects(List<EffectInfo> effectInfos)
		{
			Debug.Log("Applying effects...");

			foreach (EffectInfo effectInfo in effectInfos)
				ApplyEffect(effectInfo);
		}

		public static void ApplyEffect(EffectInfo effectInfo)
		{
			Debug.Log($"Applying effect: {effectInfo.Type} {effectInfo.Data} {effectInfo.ArithmeticOperator} {effectInfo.Value}");

			IEffect effect = CreateEffect(effectInfo.Type);

			if (effect != null)
				effect.Apply(effectInfo);
		}
	}
}
