namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 정련 체인의 한 단계 정의. '어떤 가공 단계(Kind)를 어떤 태도(Approach)로 거쳤는가'.
	/// 순수 값(readonly struct) — 체인 = IReadOnlyList&lt;RefiningStage&gt; 로 흘려 RefiningChain.Evaluate 에 주입.
	/// Phase 1 부터 Kind 별 가중치(SO 노출)가 붙어 단계마다 의미가 갈라질 자리.
	/// </summary>
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
