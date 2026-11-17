namespace WitchMendokusai
{
	// UIFxSettings 의 고르기 항목. 값은 에셋에 정수로 저장되므로 끝에만 추가

	/// <summary>화면을 덮고 걷는 무늬</summary>
	public enum ScreenWipeStyle
	{
		Fade = 0,
		Diamond = 1,
		Dots = 2,
		Pixels = 3,
		Blinds = 4,
		Iris = 5,
		Gate = 6,
		Clock = 7,
		Spiral = 8,
		Dissolve = 9,
		Wave = 10,
		Star = 11,
		Heart = 12,
		Crystal = 13,
		Fan = 14,
		Ripple = 15,
		Stripes = 16,
		Halftone = 17,
		Shutter = 18,
		Slash = 19,
		Glitch = 20,
		Tube = 21,
		Random = 100,
	}

	/// <summary>창과 패널이 열리고 닫히는 움직임 (USS 클래스)</summary>
	public enum UIMotionStyle
	{
		Pop = 0,
		Rise = 1,
		Drop = 2,
		Unfold = 3,
		Swing = 4,
		Zoom = 5,
		Slide = 6,
	}

	/// <summary>입자 모양 (수식 텍스처)</summary>
	public enum ParticleShape
	{
		Glow = 0,
		Sparkle = 1,
		Star = 2,
		Snow = 3,
		Ring = 4,
		Heart = 5,
		Petal = 6,
		Diamond = 7,
	}

	/// <summary>창이 열릴 때 터지는 입자의 움직임</summary>
	public enum BurstMotion
	{
		PopRise = 0,
		Fountain = 1,
		Orbit = 2,
		Fall = 3,
	}

	/// <summary>패널 뒤 떠다니는 입자의 움직임</summary>
	public enum MoteMotion
	{
		Rise = 0,
		Fall = 1,
		Drift = 2,
		Twinkle = 3,
	}

	/// <summary>창이 열릴 때 겉면 빛</summary>
	public enum ShineStyle
	{
		None = 0,
		Sweep = 1,
		DoubleSweep = 2,
		EdgeFlash = 3,
	}

	/// <summary>평상시 요소 효과. Mixed 는 매번 섞어 고름</summary>
	public enum IdleFxStyle
	{
		None = 0,
		Glint = 1,
		Twinkle = 2,
		Pulse = 3,
		Ring = 4,
		Wave = 5,
		Mixed = 100,
	}
}
