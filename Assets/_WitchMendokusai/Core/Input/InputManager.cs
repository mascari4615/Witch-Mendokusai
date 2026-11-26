using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum InputMapType
	{
		Player,
		UI,
	}

	public enum InputEventType
	{
		// Player
		[InputEvent("이동", "스킬", "<Keyboard>/leftShift")]
		Space,
		[InputEvent("이동", "점프", "<Keyboard>/space", EnabledInRelease = false)]
		Jump,
		[InputEvent("전투", "기본 공격", "<Mouse>/leftButton")]
		Click0,
		[InputEvent("전투", "보조 공격", "<Mouse>/rightButton")]
		Click1,
		[InputEvent("전투", "조준 모드 전환", "<Keyboard>/y", EnabledInRelease = false)]
		ChangeMode,
		[InputEvent("월드", "스크롤", "<Mouse>/scroll")]
		Scroll,
		[InputEvent("이동", "달리기", "<Keyboard>/ctrl", EnabledInRelease = false)]
		Sprint,
		[InputEvent("이동", "앉기", "<Keyboard>/c", EnabledInRelease = false)]
		Crouch,
		[InputEvent("월드", "건축 모드", "<Keyboard>/g", EnabledInRelease = false)]
		BuildModeToggle,
		[InputEvent("월드", "줍기", "<Keyboard>/e")]
		Gather,
		// HotbarSlot1~9는 연속 정의 유지 — UIHotbar이 (HotbarSlot1 + i) 산수에 의존
		[InputEvent("핫바", "핫바 슬롯 1", "<Keyboard>/1", EnabledInRelease = false)]
		HotbarSlot1,
		[InputEvent("핫바", "핫바 슬롯 2", "<Keyboard>/2", EnabledInRelease = false)]
		HotbarSlot2,
		[InputEvent("핫바", "핫바 슬롯 3", "<Keyboard>/3", EnabledInRelease = false)]
		HotbarSlot3,
		[InputEvent("핫바", "핫바 슬롯 4", "<Keyboard>/4", EnabledInRelease = false)]
		HotbarSlot4,
		[InputEvent("핫바", "핫바 슬롯 5", "<Keyboard>/5", EnabledInRelease = false)]
		HotbarSlot5,
		[InputEvent("핫바", "핫바 슬롯 6", "<Keyboard>/6", EnabledInRelease = false)]
		HotbarSlot6,
		[InputEvent("핫바", "핫바 슬롯 7", "<Keyboard>/7", EnabledInRelease = false)]
		HotbarSlot7,
		[InputEvent("핫바", "핫바 슬롯 8", "<Keyboard>/8", EnabledInRelease = false)]
		HotbarSlot8,
		[InputEvent("핫바", "핫바 슬롯 9", "<Keyboard>/9", EnabledInRelease = false)]
		HotbarSlot9,

		// UI
		[InputEvent("UI 탐색", "확인", "<Keyboard>/z")]
		Submit,
		[InputEvent("UI 탐색", "취소", "<Keyboard>/x")]
		Cancel,
		[InputEvent("카메라", "시점 조작 모드", "<Keyboard>/tab")]
		CameraControlModeToggle,
		[InputEvent("카메라", "1인칭/3인칭", "<Keyboard>/f5", EnabledInRelease = false)]
		CameraPerspectiveToggle,
		[InputEvent("카메라", "시점 순환", "<Keyboard>/f6", EnabledInRelease = false)]
		CameraViewCycle,
		[InputEvent("창", "스탯", "<Keyboard>/v")]
		Status,
		[InputEvent("창", "인벤토리", "<Keyboard>/i")]
		Inventory,
		[InputEvent("창", "개발자 창", "<Keyboard>/slash", EnabledInRelease = false)]
		DevWindowToggle,
		[InputEvent("창", "도감", "<Keyboard>/b")]
		DiscoveryToggle,
		[InputEvent("창", "퀘스트", "<Keyboard>/j")]
		QuestToggle,
		[InputEvent("창", "인형", "<Keyboard>/k")]
		DollToggle,
		[InputEvent("창", "단축키 안내", "<Keyboard>/f1", Rebindable = false)]
		KeybindHelpToggle,
		[InputEvent("창", "마도서", "<Keyboard>/m")]
		MagicBookToggle,
		[InputEvent("창", "솥 지도", "<Keyboard>/n", EnabledInRelease = false)]
		CauldronMapToggle,
		[InputEvent("대화", "건너뛰기", "<Keyboard>/p")]
		DialogueSkip,
		[InputEvent("대화", "자동 진행", "<Keyboard>/o")]
		DialogueAuto,
		[InputEvent("대화", "대화 기록", "<Keyboard>/l")]
		DialogueLog,
		[InputEvent("월드", "큰 지도 (누르는 동안)", "<Keyboard>/t", EnabledInRelease = false)]
		MinimapLargeView,
		// 맨 끝에 붙임. 중간에 넣으면 숫자로 저장된 자산 (MobileControlIcons 등) 이 밀림
		[InputEvent("창", "업적", "<Keyboard>/h")]
		AchievementToggle,
		[InputEvent("전투", "큰 포화", "<Keyboard>/r")]
		Barrage,
		[InputEvent("건설", "돌리기", "<Keyboard>/r", EnabledInRelease = false)]
		VillageTurn,
		[InputEvent("건설", "거두기", "<Keyboard>/f", EnabledInRelease = false)]
		VillageRemove,
		[InputEvent("건설", "되돌리기", "<Keyboard>/u", EnabledInRelease = false)]
		VillageUndo,
	}

	public enum InputEventResponseType
	{
		Started,
		Performed,
		Canceled,
		Get, // Custom
	}

	public enum InputAxisType
	{
		Move,
		CameraRotate,
		Look,
		// TASK-WM-193 — 자유 위치 카메라 전용 축 (플레이어 Move 와 분리, 모드별 배타 라우팅).
		CameraMove,      // 부감 pan / 자유비행 수평 (WASD raw)
		CameraVertical,  // 자유비행 상하 (Space=상승 / Shift=하강)
		ScrollWheel,     // 부감 높이 줌 (스크롤 델타)
	}

	public partial class InputManager : MonoBehaviour
	{
		public static InputManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out InputManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}
		private IInputStrategy CurrentInputStrategy { get; set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			Init();
		}

		private void OnDestroy()
		{
			DisposeRebind();
			if (Instance == this)
				Instance = null;
		}

		private void Init()
		{
			inputActionAsset.Enable();

			InitEventDictionaries();
			BindEvents();
			LoadKeybinds();

			KeybindRegistry.ValidateAgainstAsset(inputActionAsset);
			SetInputStrategy(new InputStrategyLoading());
		}

		private void Update()
		{
			UpdatePointer();
			UpdateMouseWorldPosition();
			UpdateIsPointerOverUI();
			UpdateMoveInput();
			UpdateCameraRotateInput();
			UpdateLookInput();
			UpdateCameraMoveInput();
			UpdateCameraVerticalInput();
			UpdateScrollWheelInput();
			UpdateCameraBoost();
		}
	}
}
