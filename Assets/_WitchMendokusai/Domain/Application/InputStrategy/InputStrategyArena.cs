using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 투기장 관전 입력 전략 (TASK-WM-165 item9). InputStrategyWorld 미러지만 *교체* 의미:
	/// 플레이어 전투/이동 이벤트를 전부 제거(관전자는 조작 X) + 카메라 조작만 유지.
	/// ArenaModeController 가 GameMode.Arena 진입 시 SetInputStrategy(new InputStrategyArena()),
	/// 이탈 시 new InputStrategyWorld() 로 복귀. 씬 단위(InputStrategySelector)가 아닌 모드 단위 스왑.
	/// v1 관전 = **고정 카메라뷰** — Move / CameraRotate / Look **전 축**을 IsSpectating 으로 차단한다
	/// (사유는 AxisReturnConditions 주석: 플레이어 카메라 리그 결합 회피). 남는 조작은 줌·모드토글·시점토글·나가기뿐.
	/// ⚠ 예전 이 줄은 「시점(CameraRotate/Look)은 유지」라고 적혀 있었다 — 설계가 바뀐 뒤에도 요약만 남아
	///   코드와 반대말을 하고 있었다. 여기 룰은 「주석이 계약처럼 읽히는 곳부터 의심하라」다.
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

						#region UI (X = 투기장 나가기 → 일반 모드 복귀)
						// 관전 이탈 = SetMode(Default) → ArenaModeController.ApplyMode 가 매치 정리+카메라/입력 복귀.
						// 입력 이벤트라 매치 Tick 밖에서 실행 → mid-Tick teardown 타이밍 안전.
						new(
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => GameModeManager.Instance.SetMode(GameMode.Default),
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
			// v1 관전 = 고정 카메라뷰(autobattler 방송 스타일) — 전 축 차단(IsSpectating = GameMode.Arena 파생,
			// 이 전략 활성 동안 항상 true). 플레이어 카메라 리그(원점 기준 추종)에 정적 아레나 카메라를 얹어
			// spectator 회전 시 아레나가 아닌 원점 기준 orbit 되는 리그 결합 회피. 자유 관전 카메라(아레나 중심
			// 궤도)는 전용 리그(후속). 이동(Move)은 본디 관전 중 불가.
			{
				InputAxisType.Move,
				new[] { GameConditionType.IsSpectating }
			},
			{
				InputAxisType.CameraRotate,
				new[] { GameConditionType.IsSpectating }
			},
			{
				InputAxisType.Look,
				new[] { GameConditionType.IsSpectating }
			}
		};
	}
}
