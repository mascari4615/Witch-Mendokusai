using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class GameManager : Singleton<GameManager>
	{
		public GameCondition Conditions { get; private set; } = new GameCondition();

		// 게임 상태 초기화
		public void Init()
		{
			ObjectBufferManager.ClearObjects(ObjectType.Drop);
			ObjectBufferManager.ClearObjects(ObjectType.Monster);
			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			ObjectBufferManager.ClearObjects(ObjectType.SpawnCircle);

			Player.Instance.Object.Init(GetDoll(DataManager.Instance.CurDollID));

			QuestManager.Instance.RemoveQuests(QuestType.Dungeon);
			DataManager.Instance.GameStat.UpdateData();
		}

		public void InitEquipment()
		{
			List<EquipmentData> equipments = DataManager.Instance.GetEquipmentData(DataManager.Instance.CurDollID);
			foreach (EquipmentData equipment in equipments)
			{
				if (equipment == null)
					continue;

				Effect.ApplyEffects(equipment.Effects);

				if (equipment.Object != null)
				{
					GameObject g = ObjectPoolManager.Instance.Spawn(equipment.Object);

					if (g.TryGetComponent(out SkillObject skillObject))
						skillObject.InitContext(Player.Instance.Object);

					g.SetActive(true);
				}
			}
		}
	}

	[Flags]
	public enum GameConditionType
	{
		IsPaused = 1 << 0,
		IsChatting = 1 << 1,

		IsMouseOnUI = 1 << 2,

		IsDashing = 1 << 3,
		IsPlayerCasting = 1 << 4,
		IsDied = 1 << 5,

		IsBuilding = 1 << 6,
	}

	public class GameCondition
	{
		public bool this[GameConditionType conditionType]
		{
			get
			{
				return gameConditionActions[conditionType].Invoke();
			}
		}

		private static Dictionary<GameConditionType, Func<bool>> gameConditionActions = new()
		{
			{ GameConditionType.IsPaused, () => TimeManager.Instance.IsPaused },
			{ GameConditionType.IsChatting, () => UIChat.IsChatting },

			{ GameConditionType.IsMouseOnUI, () => InputManager.Instance.IsPointerOverUI() },

			{ GameConditionType.IsDashing, () => Player.Instance.Object.UnitStat[UnitStatType.FORCE_MOVE] > 0 },
			{ GameConditionType.IsPlayerCasting, () => Player.Instance.Object.UnitStat[UnitStatType.CASTING_SKILL] > 0 },
			{ GameConditionType.IsDied, () => Player.Instance.Object.UnitStat[UnitStatType.HP_CUR] <= 0 },

			{ GameConditionType.IsBuilding, () => BuildManager.Instance.IsBuilding },
		};
		
		public bool CheckGameCondition(GameConditionType gameCondition)
		{
			foreach (KeyValuePair<GameConditionType, Func<bool>> condition in gameConditionActions)
			{
				if (gameCondition.HasFlag(condition.Key) && condition.Value.Invoke())
					return false;
			}

			return true;
		}
	}
}