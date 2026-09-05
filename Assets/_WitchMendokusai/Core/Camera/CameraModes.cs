namespace WitchMendokusai
{
	// 카메라 2축 직교 매트릭스 (TASK-WM-163).
	// 시점 조작 방식(ControlMode) × 시점 위치(Perspective) = 4 조합.

	/// <summary>
	/// 시점 조작 방식. PointAndClick = 현행 (커서 자유, Q/E 로 yaw 회전).
	/// MouseLook = 마인크래프트식 (마우스 이동 = 시야 회전, 커서 잠금).
	/// </summary>
	public enum CameraControlMode
	{
		PointAndClick = 0,
		MouseLook = 1,
	}

	/// <summary>
	/// 시점 위치. ThirdPerson = 현행 (캐릭터 뒤). FirstPerson = 머리 위치 (자기 스프라이트 숨김).
	/// </summary>
	public enum CameraPerspective
	{
		ThirdPerson = 0,
		FirstPerson = 1,
	}
}
