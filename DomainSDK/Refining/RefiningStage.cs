namespace WitchMendokusai.DomainSDK.Refining
{
	public readonly struct RefiningStage
	{
		public readonly RefiningStageKind Kind;
		public readonly RefiningApproach Approach;

		public RefiningStage(RefiningStageKind kind, RefiningApproach approach)
		{
			Kind = kind;
			Approach = approach;
		}
	}
}
