namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 한 정련 단계에 임하는 태도. '함부로 vs 애도하며'의 코어 축.
	/// Fast(빨리·함부로) → 빨리 끝내려 거칠게: 품질 상승 적고 마을 온기↓. 링(충동)의 색.
	/// Careful(애도하며·정성껏) → 죽은 것을 존중하며 천천히: 품질 상승 크고 온기↑. 알리사(질서)의 색.
	/// 두 태도가 MDD(욘이 죽음을 다루는 두 얼굴)에 정합. 효율 손익 X — 톤·서사가 함께 갈리는 선택.
	/// </summary>
	public enum RefiningApproach
	{
		Fast = 0,
		Careful = 1,
	}
}
