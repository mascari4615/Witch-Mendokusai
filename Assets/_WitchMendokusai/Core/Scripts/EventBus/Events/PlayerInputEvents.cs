namespace WitchMendokusai
{
	public struct PlayerJumpRequestedEvent
	{
	}

	public struct PlayerJumpReleasedEvent
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
