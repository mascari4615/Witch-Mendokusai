using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 입력 전략 (TASK-WM-194 증분3). InputStrategyArena 미러지만 *상호작용형* —
	/// 관전(Arena)과 달리 우클릭=타워 배치/좌클릭=채집건물 배치를 실제로 구동한다. 플레이어(캐릭터)는
	/// 이 모드에 존재하지 않으므로 이동/전투 이벤트는 Arena 와 동형으로 전부 미등록 + 축은
	/// IsTowerDefenseMode 로 차단(IsSpectating 대칭, GameConditionType.cs/GameManager.cs 참조).
	/// TowerDefenseModeController 가 GameMode.TowerDefense 진입 시 SetInputStrategy(new InputStrategyTowerDefense(...)),
	/// 이탈 시 new InputStrategyWorld() 로 복귀.
	/// </summary>
	public class InputStrategyTowerDefense : InputStrategyBase
	{
		/// <summary> 개척 배치 정책 — 한 클릭에 한 개(<see cref="PlacementInputMode.SingleClick"/>). </summary>
		public const PlacementInputMode PLACEMENT_MODE = PlacementInputMode.SingleClick;

		// Performed 로 이미 1회지만, 같은 프레임 중복 디스패치·더블클릭 튐 방지용 최소 간격.
		private const float CLICK_COOLDOWN = 0.1f;

		private readonly TowerDefensePlacement placement;
		private readonly InputManager inputManager;
		private float lastClickTime;

		public InputStrategyTowerDefense(TowerDefensePlacement placement, InputManager inputManager)
		{
			this.placement = placement;
			this.inputManager = inputManager;
		}

		private List<InputRegisterData> _inputRegisterDataList;
		public override List<InputRegisterData> InputRegisterDataList
		{
			get
			{
				_inputRegisterDataList ??= new List<InputRegisterData>()
					{
						#region Camera (Arena 와 동일 세트 — 관전/개척 공통 카메라 조작)
						new(
							InputEventType.Scroll,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.Zoom(),
							() => CanExecute(InputEventType.Scroll)
						),
						new(
							InputEventType.CameraControlModeToggle,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.ToggleControlMode(),
							() => CanExecute(InputEventType.CameraControlModeToggle)
						),
						new(
							InputEventType.CameraPerspectiveToggle,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.TogglePerspective(),
							() => CanExecute(InputEventType.CameraPerspectiveToggle)
						),
						#endregion

						#region Placement (BuildManager.ApplyMode 동형 — Get 단위 폴 + 쿨다운/UI 가드는 콜백 내부)
						// PlacementInputMode.SingleClick — Get(매 프레임 폴)이면 버튼을 누르고 있는 동안
						// 계속 설치돼 드래그로 죽 깔린다(월드 건설은 그게 맞지만 비용이 붙는 개척 배치엔 사고).
						// Performed = 누르는 동작당 1회 → "한 클릭에 한 개".
						new(
							InputEventType.Click1,
							InputEventResponseType.Performed,
							() => HandlePlaceTowerClick(),
							() => CanExecute(InputEventType.Click1)
						),
						new(
							InputEventType.Click0,
							InputEventResponseType.Performed,
							() => HandlePlaceHarvesterClick(),
							() => CanExecute(InputEventType.Click0)
						),
						#endregion

						#region UI (Cancel = 개척 나가기 → 일반 모드 복귀, Arena 동형)
						new(
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => GameModeManager.Instance.SetMode(GameMode.Default),
							() => CanExecute(InputEventType.Cancel)
						),
						#endregion

						// 플레이어 이벤트(Space/Jump/Sprint/Crouch/ChangeMode/BuildModeToggle/Submit) = 의도적 미등록
						// — 이 모드엔 플레이어 캐릭터가 없다(Arena 관전과 동형 원칙).
					};

				return _inputRegisterDataList;
			}
		}

		// BuildManager.ClickCell/TryRemoveCell 동형 — UI 위 클릭 무시 + 쿨다운(한 클릭 다중 배치 방지).
		private void HandlePlaceTowerClick()
		{
			if (inputManager.IsPointerOverUI())
				return;
			if (Time.time - lastClickTime < CLICK_COOLDOWN)
				return;

			lastClickTime = Time.time;
			placement.PlaceTowerAt(inputManager.MouseScreenPosition);
		}

		private void HandlePlaceHarvesterClick()
		{
			if (inputManager.IsPointerOverUI())
				return;
			if (Time.time - lastClickTime < CLICK_COOLDOWN)
				return;

			lastClickTime = Time.time;
			placement.PlaceHarvesterAt(inputManager.MouseScreenPosition);
		}

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{ InputEventType.Scroll, new[] { GameConditionType.IsTyping } },
			{ InputEventType.CameraControlModeToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{ InputEventType.CameraPerspectiveToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{
				InputEventType.Click1,
				new[] { GameConditionType.IsMouseOnUI, GameConditionType.IsTyping, GameConditionType.IsPaused }
			},
			{
				InputEventType.Click0,
				new[] { GameConditionType.IsMouseOnUI, GameConditionType.IsTyping, GameConditionType.IsPaused }
			},
			{ InputEventType.Cancel, new[] { GameConditionType.IsTyping } },
		};

		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new()
		{
			// 플레이어 캐릭터가 이 모드에 없음 — 이동/시점 축 전부 차단(IsSpectating 대칭, TASK-WM-194).
			{ InputAxisType.Move, new[] { GameConditionType.IsTowerDefenseMode } },
			{ InputAxisType.CameraRotate, new[] { GameConditionType.IsTowerDefenseMode } },
			{ InputAxisType.Look, new[] { GameConditionType.IsTowerDefenseMode } },
		};
	}
}
