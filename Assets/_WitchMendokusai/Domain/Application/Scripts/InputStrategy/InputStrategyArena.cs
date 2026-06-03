using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 투기장 관전 입력 전략 (TASK-WM-165 item9). InputStrategyWorld 미러지만 *교체* 의미:
	/// 플레이어 전투/이동 이벤트를 전부 제거(관전자는 조작 X) + 카메라 조작만 유지.
	/// ArenaModeController 가 GameMode.Arena 진입 시 SetInputStrategy(new InputStrategyArena()),
	/// 이탈 시 new InputStrategyWorld() 로 복귀. 씬 단위(InputStrategySelector)가 아닌 모드 단위 스왑.
	/// 이동(Move 축)은 IsSpectating 으로 차단(관전 중 플레이어 이동 불가), 시점(CameraRotate/Look)은 유지.
	/// </summary>
	public class InputStrategyArena : InputStrategyBase
	{
		private List<InputRegisterData> _inputRegisterDataList;
		public override List<InputRegisterData> InputRegisterDataList
		{
			get
			{
				_inputRegisterDataList ??= new List<InputRegisterData>()
					{
						#region Camera (관전 카메라 조작 — 유지)
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

						#region UI (취소만 — 관전 중 패널 닫기)
						new(
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => UIManager.Instance.OnCancelInput(),
							() => CanExecute(InputEventType.Cancel)
						),
						#endregion

						// 플레이어 이벤트(Space/Jump/Click0/Click1/Sprint/Crouch/ChangeMode/BuildModeToggle/Submit)
						// = 의도적으로 미등록 → 관전 중 전투·상호작용 입력 무효 (strategyEvents 가 매 스왑 clear 됨).
					};

				return _inputRegisterDataList;
			}
		}

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{ InputEventType.Scroll, new[] { GameConditionType.IsTyping } },
			{ InputEventType.CameraControlModeToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{ InputEventType.CameraPerspectiveToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{ InputEventType.Cancel, new[] { GameConditionType.IsTyping } },
		};

		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new()
		{
			// 이동 = 관전 중 차단 (IsSpectating = GameMode.Arena 파생 → 이 전략 활성 동안 항상 true).
			{
				InputAxisType.Move,
				new[]
				{
					GameConditionType.IsSpectating,
				}
			},
			// 시점 회전/마우스룩 = 관전자 카메라 자유 (일반 게이트만 — 일시정지/타이핑/전환/UI).
			{
				InputAxisType.CameraRotate,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			},
			{
				InputAxisType.Look,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			}
		};
	}
}
