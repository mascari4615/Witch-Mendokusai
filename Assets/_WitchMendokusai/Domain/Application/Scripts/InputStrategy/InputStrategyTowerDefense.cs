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
		// 시간 조작은 매치가 쥔다 — 입력은 「눌렸다」만 전하고 규칙은 매치에 남는다.
		private readonly TowerDefenseMatch match;
		// 지도 여닫기 — 입력이 화면을 직접 쥐면 층이 꼬인다(입력은 「눌렸다」만 전한다).
		private readonly System.Action toggleMap;
		// 취소(X) — 창을 닫는 뜻. 판을 나가는 것은 여기 없다.
		private readonly System.Action cancelPressed;
		private float lastClickTime;

		public InputStrategyTowerDefense(TowerDefensePlacement placement, InputManager inputManager, TowerDefenseMatch match,
			System.Action toggleMap = null, System.Action cancelPressed = null)
		{
			this.placement = placement;
			this.inputManager = inputManager;
			this.match = match;
			this.toggleMap = toggleMap;
			this.cancelPressed = cancelPressed;
		}

		private List<InputRegisterData> _inputRegisterDataList;
		public override List<InputRegisterData> InputRegisterDataList
		{
			get
			{
				_inputRegisterDataList ??= new List<InputRegisterData>()
					{
						// 카메라 이벤트(Scroll→CameraManager.Zoom / 시점·투영 토글) = 의도적 미등록.
						// 개척은 자기 부감 카메라(OverheadCameraRig)를 직접 구동한다 — CameraManager 의
						// 플레이어 추종 리그에 줌을 걸면 화면엔 아무 변화가 없으면서 *본편 카메라 상태만*
						// 조용히 바뀌어 개척을 나간 뒤에 드러난다. 휠 줌은 ScrollWheel 축으로 리그가 직접 읽는다.

						#region Placement (BuildManager.ApplyMode 동형 — Get 단위 폴 + 쿨다운/UI 가드는 콜백 내부)
						// PlacementInputMode.SingleClick — Get(매 프레임 폴)이면 버튼을 누르고 있는 동안
						// 계속 설치돼 드래그로 죽 깔린다(월드 건설은 그게 맞지만 비용이 붙는 개척 배치엔 사고).
						// Performed = 누르는 동작당 1회 → "한 클릭에 한 개".
						// 좌클릭 = 핫바에서 고른 것 설치(클릭 1회 = 1개).
						// ★ 손가락일 땐 이 길이 닫힌다 (TASK-WM-200). 손가락은 「톡 두 번」으로 짓는데
						//   마우스 길이 살아 있으면 *한 번 누른 순간* 지어져 버려서, 컴퓨터에서 폰 조작을
						//   켜고 확인할 때 실제 폰과 다르게 동작한다 — 확인 도구가 거짓말을 하면
						//   그 뒤로 무엇을 확인해도 못 믿는다. 진짜 폰엔 마우스가 없어 어차피 안 온다.
						new(
							InputEventType.Click0,
							InputEventResponseType.Performed,
							() => HandlePlaceClick(),
							() => CanExecute(InputEventType.Click0) && inputManager.IsTouchMode == false
						),
						#endregion

						new(
							InputEventType.Click1,
							InputEventResponseType.Performed,
							() => placement.SellAt(inputManager.MouseScreenPosition),
							() => CanExecute(InputEventType.Click1) && inputManager.IsTouchMode == false
						),
						// ★ 우클릭을 *누르고 있는 동안* 영웅이 계속 커서를 따라온다 (사용자 요청).
						//   한 번만 되면 「저기로 가」를 매번 다시 눌러야 해서, 영웅을 데리고 다니는 게
						//   명령의 연속이 아니라 클릭 노동이 된다. RTS 의 우클릭 끌기와 같은 감각.
						//   ★ 설치 대기 중일 땐 안 건다 — 그때 우클릭은 「판다」라서, 끌면 지나간 자리의
						//     건물이 줄줄이 팔린다(되돌릴 수 없는 일이 손에 딸려 오면 안 된다).
						new(
							InputEventType.Click1,
							InputEventResponseType.Get,
							() => placement.CommandHeroAt(inputManager.MouseScreenPosition),
							() => CanExecute(InputEventType.Click1) && inputManager.IsTouchMode == false
								&& placement.IsArmed == false
						),
						// 시간 조작 — 이미 있는 입력 슬롯 재사용(새 입력 추가는 3곳 동시 수정이라 값이 더 크다).
						new(
							InputEventType.Space,
							InputEventResponseType.Performed,
							() => match.TogglePause(),
							() => CanExecute(InputEventType.Space)
						),
						new(
							InputEventType.CameraViewCycle,
							InputEventResponseType.Performed,
							() => match.CycleSpeed(),
							() => CanExecute(InputEventType.CameraViewCycle)
						),
						// 지도 — M. **이미 있는 슬롯을 빌린다**(마도서 키). 개척에선 마도서 조작을 등록하지
						// 않으므로 겹치지 않고, 새 입력을 만들면 열거·바인딩 표·입력 에셋 3곳을 동시에
						// 고쳐야 한다 — 실제로 그 에셋을 고치다 식별자 형식 하나 때문에 입력이 통째로
						// 안 읽혀 부팅이 죽었다(되돌림). 배속이 카메라 슬롯을 빌려 쓰는 것과 같은 방식이다.
						new(
							InputEventType.MagicBookToggle,
							InputEventResponseType.Performed,
							() => toggleMap?.Invoke(),
							() => toggleMap != null
						),
						#region Hotbar (기존 건설 모드와 같은 조작 문법 — 숫자키로 설치 대상 선택)
						new(
							InputEventType.HotbarSlot1,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(0),
							() => CanExecute(InputEventType.HotbarSlot1)
						),
						new(
							InputEventType.HotbarSlot2,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(1),
							() => CanExecute(InputEventType.HotbarSlot2)
						),
						new(
							InputEventType.HotbarSlot3,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(2),
							() => CanExecute(InputEventType.HotbarSlot3)
						),
						new(
							InputEventType.HotbarSlot4,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(3),
							() => CanExecute(InputEventType.HotbarSlot4)
						),
						new(
							InputEventType.HotbarSlot5,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(4),
							() => CanExecute(InputEventType.HotbarSlot5)
						),
						new(
							InputEventType.HotbarSlot6,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(5),
							() => CanExecute(InputEventType.HotbarSlot6)
						),
						new(
							InputEventType.HotbarSlot7,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(6),
							() => CanExecute(InputEventType.HotbarSlot7)
						),
						new(
							InputEventType.HotbarSlot8,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(7),
							() => CanExecute(InputEventType.HotbarSlot8)
						),
						new(
							InputEventType.HotbarSlot9,
							InputEventResponseType.Performed,
							() => placement.SelectSlot(8),
							() => CanExecute(InputEventType.HotbarSlot9)
						),
						#endregion

						#region UI (Cancel = 개척 나가기 → 일반 모드 복귀, Arena 동형)
						new(
							// ★ X 로 판을 나가지 않는다(사용자 지시) — 한 번 잘못 누르면 진행 중인 판이
							//   통째로 끝나는데, 되돌릴 방법이 없다. 취소는 *창을 닫는* 뜻으로만 쓴다.
							//   판을 나가는 것은 모서리 「나가기」 버튼처럼 의도가 분명한 손짓이어야 한다.
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => cancelPressed?.Invoke(),
							() => cancelPressed != null
						),
						#endregion

						// 플레이어 이벤트(Space/Jump/Sprint/Crouch/ChangeMode/BuildModeToggle/Submit) = 의도적 미등록
						// — 이 모드엔 플레이어 캐릭터가 없다(Arena 관전과 동형 원칙).
					};

				return _inputRegisterDataList;
			}
		}

		// BuildManager.ClickCell/TryRemoveCell 동형 — UI 위 클릭 무시 + 쿨다운(한 클릭 다중 배치 방지).
		private void HandlePlaceClick()
		{
			if (inputManager.IsPointerOverUI())
				return;
			if (Time.time - lastClickTime < CLICK_COOLDOWN)
				return;

			lastClickTime = Time.time;
			placement.PlaceSelectedAt(inputManager.MouseScreenPosition);
		}

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{
				InputEventType.Click0,
				new[] { GameConditionType.IsMouseOnUI, GameConditionType.IsTyping, GameConditionType.IsPaused }
			},
			{ InputEventType.Click1, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.Space, new[] { GameConditionType.IsTyping } },
			{ InputEventType.CameraViewCycle, new[] { GameConditionType.IsTyping } },
			{ InputEventType.HotbarSlot1, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot2, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot3, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot4, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot5, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot6, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot7, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot8, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
			{ InputEventType.HotbarSlot9, new[] { GameConditionType.IsTyping, GameConditionType.IsPaused } },
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
