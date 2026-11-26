namespace WitchMendokusai
{
	public struct PlayerJumpRequestedEvent
	{
	}

	public struct PlayerJumpReleasedEvent
	{
	}

	/// <summary> 직접 누르는 큰 포화 </summary>
	public struct PlayerBarrageRequestedEvent
	{
	}

	public struct PlayerSkillUseRequestedEvent
	{
		public int SkillIndex;
	}

	public struct PlayerSprintChangedEvent
	{
		public bool IsSprinting;
	}

	public struct PlayerCrouchChangedEvent
	{
		public bool IsCrouching;
	}

	public struct PlayerAutoAimToggledEvent
	{
	}

	public struct PlayerInteractRequestedEvent
	{
	}
}
