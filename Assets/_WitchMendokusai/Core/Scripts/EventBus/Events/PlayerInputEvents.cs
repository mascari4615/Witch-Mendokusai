namespace WitchMendokusai
{
	public struct PlayerJumpRequestedEvent : IEvent
	{
	}

	public struct PlayerJumpReleasedEvent : IEvent
	{
	}

	public struct PlayerSkillUseRequestedEvent : IEvent
	{
		public int SkillIndex;
	}

	public struct PlayerSprintChangedEvent : IEvent
	{
		public bool IsSprinting;
	}

	public struct PlayerCrouchChangedEvent : IEvent
	{
		public bool IsCrouching;
	}

	public struct PlayerAutoAimToggledEvent : IEvent
	{
	}

	public struct PlayerInteractRequestedEvent : IEvent
	{
	}
}
