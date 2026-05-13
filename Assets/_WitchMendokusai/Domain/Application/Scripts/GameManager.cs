using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class GameManager : MonoBehaviour
	{
		public static GameManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out GameManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public GameCondition Conditions { get; private set; }

		private InputManager inputManager;
		private UnitObject playerObject;

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			Conditions = new GameCondition(() => playerObject, inputManager);
			GameConditionBridge.Register(Conditions);

			SOManager soManager = SOManager.Instance;
			FloatVariable joystickX = soManager.JoystickX;
			FloatVariable joystickY = soManager.JoystickY;
			JoystickBridge.GetX = () => joystickX.RuntimeValue;
			JoystickBridge.GetY = () => joystickY.RuntimeValue;
			WindowLayoutBridge.Register(soManager.WindowLayoutData);

			IEventBus eventBus = EventBusBridge.Instance;
			eventBus.Subscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
			eventBus.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		private void OnDestroy()
		{
			if (EventBusBridge.TryGetInstance(out IEventBus eventBus))
			{
				eventBus.Unsubscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
				eventBus.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			}

			if (Instance == this)
				Instance = null;
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

	public class GameCondition : IGameConditionBridge
	{
		private readonly Func<UnitObject> getPlayerObject;
		private readonly Dictionary<GameConditionType, Func<bool>> gameConditionActions;

		public GameCondition(Func<UnitObject> getPlayerObject, InputManager inputManager)
		{
			this.getPlayerObject = getPlayerObject;

			gameConditionActions = new()
			{
				{ GameConditionType.IsPaused, () => TimeManager.Instance.IsPaused }, // Setting, Dungeon Card 선택, Transition, ...
				{ GameConditionType.IsTyping, () => UIChat.IsChatting || (DevWindowController.TryGetExistingInstance(out DevWindowController dwc) && dwc.IsCommandLineFocused) || UIToolkitFocus.IsAnyTextFieldFocused() },
				{ GameConditionType.IsMouseOnUI, () => inputManager.IsPointerOverUI() },
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
