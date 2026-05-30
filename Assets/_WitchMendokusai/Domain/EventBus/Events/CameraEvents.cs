namespace WitchMendokusai
{
	// 카메라 모드 변화 알림 (TASK-WM-163). EventBus 디커플 — CameraManager publish,
	// 관심 컴포넌트(PlayerObject 자기 스프라이트 숨김 등) subscribe.

	public struct CameraControlModeChangedEvent
	{
		public CameraControlMode Mode;
	}

	public struct CameraPerspectiveChangedEvent
	{
		public CameraPerspective Perspective;
		public bool IsFirstPerson;
	}
}
