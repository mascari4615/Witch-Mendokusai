namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 그래프 도메인 — 어느 게임 시스템 (지형 / 마도서 / 퀘스트 ...) 의 그래프냐.
	/// `NodeRegistry` 가 카탈로그 필터링 시 사용 — 지형 그래프 카탈로그 = Terrain 도메인 노드만.
	/// `Generic` 은 fallback / 마이그레이션 — 도메인 미명시 NodeGraph 인스턴스 는 모든 노드 카탈로그 (호환성).
	/// </summary>
	public enum NodeDomain
	{
		Generic = 0,
		Terrain = 1,
		MagicBook = 2,
	}
}
