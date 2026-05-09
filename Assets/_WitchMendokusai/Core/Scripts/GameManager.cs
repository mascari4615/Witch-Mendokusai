using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class GameManager : Singleton<GameManager>
	{
		public GameCondition Conditions { get; private set; }

		private UnitObject playerObject;

		protected override void Awake()
		{
			base.Awake();

			Conditions = new GameCondition(() => playerObject);

			EventBus eventBus = EventBus.Instance;
			eventBus.Subscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
			eventBus.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		protected override void OnDestroy()
		{
			if (EventBus.TryGetExistingInstance(out EventBus eventBus))
			{
				eventBus.Unsubscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
				eventBus.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			}

			base.OnDestroy();
		}

		private void OnPlayerObjectBound(PlayerObjectBoundEvent evt) => playerObject = evt.Object;
		private void OnPlayerDespawned(PlayerDespawnedEvent evt) => playerObject = null;

		// 게임 상태 초기화
		public void Init()
		{
			ObjectBufferManager.ClearObjects(ObjectType.Drop);
			ObjectBufferManager.ClearObjects(ObjectType.Monster);
			ObjectBufferManager.ClearObjects(ObjectType.ResourceNode);
			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			ObjectBufferManager.ClearObjects(ObjectType.SpawnCircle);

			playerObject.Init(GetDoll(DataManager.Instance.CurDollID));

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
						skillObject.InitContext(new SkillContext(playerObject));

					g.SetActive(true);
				}
			}
		}

		public void ApplyUpgradeEffects()
		{
			List<UpgradeData> upgrades = SOManager.Instance.DataSOs[typeof(UpgradeData)].Values.Cast<UpgradeData>().ToList();
			foreach (UpgradeData upgrade in upgrades)
			{
				if (upgrade.CurLevel <= 0)
					continue;

				upgrade.Apply();
			}
		}
	}

	public enum GameConditionType
	{
		IsPaused = 1 << 0,
		// 텍스트 입력 중 (chat OR dev console 등) — 게임 입력 차단용 단일 게이트
		IsTyping = 1 << 1,

		IsMouseOnUI = 1 << 2,

		IsPlayerCasting = 1 << 3,
		IsDied = 1 << 4,

		IsBuilding = 1 << 5,
		IsInTransition = 1 << 6,
		IsViewingUI = 1 << 7, // 전체화면 UI를 보는 중
	}

	public class GameCondition
	{
		private readonly Func<UnitObject> getPlayerObject;
		private readonly Dictionary<GameConditionType, Func<bool>> gameConditionActions;

		public GameCondition(Func<UnitObject> getPlayerObject)
		{
			this.getPlayerObject = getPlayerObject;

			gameConditionActions = new()
			{
				{ GameConditionType.IsPaused, () => TimeManager.Instance.IsPaused }, // Setting, Dungeon Card 선택, Transition, ...
				{ GameConditionType.IsTyping, () => UIChat.IsChatting || (DevWindowController.TryGetExistingInstance(out DevWindowController dwc) && dwc.IsCommandLineFocused) || UIToolkitFocus.IsAnyTextFieldFocused() },
				{ GameConditionType.IsMouseOnUI, () => InputManager.Instance.IsPointerOverUI() },
				{ GameConditionType.IsPlayerCasting, IsPlayerCasting },
				{ GameConditionType.IsDied, IsDied },
				{ GameConditionType.IsBuilding, () => GameModeManager.Instance.IsBuildMode },
				{ GameConditionType.IsInTransition, () => UITransition.IsInTransition },
				{ GameConditionType.IsViewingUI, () => UIManager.Instance.IsAnyPanelFullscreenOpen },
			};
		}

		private bool IsPlayerCasting()
		{
			UnitObject playerObject = getPlayerObject();
			return playerObject != null && playerObject.UnitStat[UnitStatType.CASTING_SKILL] > 0;
		}

		private bool IsDied()
		{
			UnitObject playerObject = getPlayerObject();
			return playerObject != null && playerObject.UnitStat[UnitStatType.HP_CUR] <= 0;
		}

		public bool this[GameConditionType conditionType]
		{
			get
			{
				return gameConditionActions[conditionType].Invoke();
			}
		}

		public bool IsGameConditionAny(params GameConditionType[] conditions)
		{
			if (conditions.Any(c => IsGameCondition(c) == true))
				return true;

			return false;
		}

		public bool IsGameCondition(GameConditionType gameCondition)
		{
			// foreach (KeyValuePair<GameConditionType, Func<bool>> condition in gameConditionActions)
			// {
			// 	if (gameCondition.HasFlag(condition.Key) && condition.Value.Invoke())
			// 		return true;
			// }

			if (gameConditionActions.ContainsKey(gameCondition) && gameConditionActions[gameCondition].Invoke())
				return true;

			return false;
		}
	}
}