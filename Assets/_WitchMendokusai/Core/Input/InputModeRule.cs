using System.Collections.Generic;

namespace WitchMendokusai
{
	// 지금 어떤 키 모드인가. 건설 (부감 건설) 에 들어가면 Build, 나오면 Normal
	// 사용자 2026-11-27 "건설모드랑 일반 상태랑 키 모드를 다르게"
	public enum InputMode
	{
		Normal = 0,
		Build = 1,
	}

	// 모드마다 어떤 입력을 보내나. InputManager.Dispatch 한 곳에서 물어서 전략, 컴포넌트 등록, 키, 패드, 화면 버튼이 같이 걸림
	// ★ 전략의 조건 목록에 하나씩 더하지 않는 이유: 줍기 (E) 와 휠처럼 컴포넌트가 직접 등록한 입력은 전략 조건을 안 지남
	public static class InputModeRule
	{
		// 건설이 다시 쓰는 Player 맵 입력. 놓기와 내려놓기 (좌우 클릭), 팔레트 칸 (1~9)
		private static readonly HashSet<InputEventType> BUILD_REUSED_PLAYER_EVENTS = new()
		{
			InputEventType.Click0,
			InputEventType.Click1,
			InputEventType.HotbarSlot1,
			InputEventType.HotbarSlot2,
			InputEventType.HotbarSlot3,
			InputEventType.HotbarSlot4,
			InputEventType.HotbarSlot5,
			InputEventType.HotbarSlot6,
			InputEventType.HotbarSlot7,
			InputEventType.HotbarSlot8,
			InputEventType.HotbarSlot9,
		};

		public static bool Allows(InputMode mode, InputEventType eventType, InputMapType mapType, InputEventResponseType responseType)
		{
			// 뗌은 늘 보냄. 달리기를 누른 채 건설에 들어가도 뗄 때 달리기가 꺼지게
			if (responseType == InputEventResponseType.Canceled)
				return true;

			if (mode == InputMode.Normal)
				return true;

			// Build: 전투와 이동 (Player 맵) 은 끄고 건설이 쓰는 것만. UI 맵 (ESC, F6, 창, 대화) 은 그대로
			if (mapType == InputMapType.UI)
				return true;
			return BUILD_REUSED_PLAYER_EVENTS.Contains(eventType);
		}
	}
}
