using System;
using System.Collections.Generic;
using System.Linq;
using MessagePipe;
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
		private DataManager dataManager;
		private ObjectPoolManager objectPoolManager;
		private TimeManager timeManager;
		private SOManager soManager;
		private IEffectRunner effectRunner;
		private UnitObject playerObject;

		private IDisposable objectBoundSub;
		private IDisposable despawnedSub;

		[Inject]
		public void Construct(InputManager inputManager, DataManager dataManager, ObjectPoolManager objectPoolManager, TimeManager timeManager, SOManager soManager,
			IEffectRunner effectRunner,
			ISubscriber<PlayerObjectBoundEvent> objectBoundSubscriber, ISubscriber<PlayerDespawnedEvent> despawnedSubscriber)
		{
			this.inputManager = inputManager;
			this.dataManager = dataManager;
			this.objectPoolManager = objectPoolManager;
			this.timeManager = timeManager;
			this.soManager = soManager;
			this.effectRunner = effectRunner;
			objectBoundSub = objectBoundSubscriber.Subscribe(OnPlayerObjectBound);
			despawnedSub = despawnedSubscriber.Subscribe(OnPlayerDespawned);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			Conditions = new GameCondition(() => playerObject, inputManager, timeManager);
			GameConditionBridge.Register(Conditions);

			FloatVariable joystickX = soManager.JoystickX;
			FloatVariable joystickY = soManager.JoystickY;
			JoystickBridge.GetX = () => joystickX.RuntimeValue;
			JoystickBridge.GetY = () => joystickY.RuntimeValue;
			WindowLayoutBridge.Register(soManager.WindowLayoutData);

		}

		private void OnDestroy()
		{
			objectBoundSub?.Dispose();
			despawnedSub?.Dispose();

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

			playerObject.Init(GetDoll(dataManager.CurDollID));

			dataManager.QuestManager.RemoveQuests(QuestType.Dungeon);
			dataManager.GameStat.UpdateData();
		}

		public void InitEquipment()
		{
			List<EquipmentData> equipments = dataManager.GetEquipmentData(dataManager.CurDollID);
			foreach (EquipmentData equipment in equipments)
			{
				if (equipment == null)
					continue;

				effectRunner.ApplyEffects(equipment.Effects);

				if (equipment.Object != null)
				{
					GameObject g = objectPoolManager.Spawn(equipment.Object);

					if (g.TryGetComponent(out SkillObject skillObject))
						skillObject.InitContext(new SkillContext(playerObject));

					g.SetActive(true);
				}
			}
		}

		// θ — SceneLifetimeScope 에서 씬 의존 조건을 Root 스코프로 바인딩 (TASK-WM-078, 2026-05-13).
		public void BindSceneConditions(GameModeManager gameModeManager, UIManager uiManager)
			=> Conditions.BindSceneDependencies(gameModeManager, uiManager);

		public void ApplyUpgradeEffects()
		{
			List<UpgradeData> upgrades = soManager.DataSOs[typeof(UpgradeData)].Values.Cast<UpgradeData>().ToList();
			foreach (UpgradeData upgrade in upgrades)
			{
				if (upgrade.CurLevel <= 0)
					continue;

				// TASK-WM-107 Slice 3-3 — static Effect 우회 폐기: DI runner 경유 dispatch.
				effectRunner.ApplyEffect(upgrade.ToEffectInfo());
			}
		}
	}

	public class GameCondition : IGameConditionBridge
	{
		private readonly Func<UnitObject> getPlayerObject;
		private readonly Dictionary<GameConditionType, Func<bool>> gameConditionActions;

		public GameCondition(Func<UnitObject> getPlayerObject, InputManager inputManager, TimeManager timeManager)
		{
			this.getPlayerObject = getPlayerObject;

			gameConditionActions = new()
			{
				{ GameConditionType.IsPaused, () => timeManager.IsPaused }, // Setting, Dungeon Card 선택, Transition, ...
				{ GameConditionType.IsTyping, () => UIChat.IsChatting || (DevWindowController.TryGetExistingInstance(out DevWindowController dwc) && dwc.IsCommandLineFocused) || UIToolkitFocus.IsAnyTextFieldFocused() },
				{ GameConditionType.IsMouseOnUI, () => inputManager.IsPointerOverUI() },
				{ GameConditionType.IsPlayerCasting, IsPlayerCasting },
				{ GameConditionType.IsDied, IsDied },
				// 씬 의존 조건 — SceneLifetimeScope.RegisterBuildCallback 에서 BindSceneDependencies 로 교체 (TASK-WM-078 θ).
				{ GameConditionType.IsBuilding, () => false },
				{ GameConditionType.IsInTransition, () => UITransition.IsInTransition },
				{ GameConditionType.IsViewingUI, () => false },
			};
		}

		// θ — Root 스코프에서 static Instance 없이 Scene 의존 조건 수신 (TASK-WM-078, 2026-05-13).
		// 호출자: SceneLifetimeScope.RegisterBuildCallback (child scope 가 parent GameManager 리졸브 후 바인딩).
		public void BindSceneDependencies(GameModeManager gameModeManager, UIManager uiManager)
		{
			gameConditionActions[GameConditionType.IsBuilding] = () => gameModeManager.IsBuildMode;
			gameConditionActions[GameConditionType.IsViewingUI] = () => uiManager.IsAnyPanelFullscreenOpen;
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
			if (gameConditionActions.ContainsKey(gameCondition) && gameConditionActions[gameCondition].Invoke())
				return true;

			return false;
		}
	}
}
