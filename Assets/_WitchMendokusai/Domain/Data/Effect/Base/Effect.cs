using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx-dispatch 인터페이스. static Effect 파사드/IContextualEffect dual
	// 폐기 완료 (모든 dispatch = IEffectRunner 경유, ctx 항상 주입). ctx 불요 Effect 는 context 무시.
	public interface IEffect
	{
		void Apply(EffectInfo effectInfo, EffectContext context);
	}

	public class Effect
	{
		// EffectInfoData → EffectInfo 해석 (dataSO lookup). EffectRunner 전용 (정적 파사드 폐기).
		public static EffectInfo ResolveEffectInfo(EffectInfoData data, EffectContext context)
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
					dataSO = context.SOManager.VQuests.Data[Random.Range(0, context.SOManager.VQuests.Data.Count)];
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

		// EffectType → IEffect 인스턴스. EffectRunner 전용 (Bridge無 — 단순 팩토리).
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
		// TASK-WM-107 Slice 3-4b — 정적 ApplyEffect/ApplyEffects 파사드 폐기 (호출처 0:
		// UpgradeData=3-3 / CardData=3-4a 라우팅). 유일 dispatch = IEffectRunner (ctx 주입).
	}
}
