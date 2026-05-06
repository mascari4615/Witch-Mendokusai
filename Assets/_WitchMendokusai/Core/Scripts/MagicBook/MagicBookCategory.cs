namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 카테고리 — `wm/design/gameplay/research.md` 의 3개 분류.
	/// SpellNode SerializeField 로 분류. UI 색상 / 페이지 그룹 / 진행 흐름 분기 등에 사용.
	/// </summary>
	public enum MagicBookCategory
	{
		/// <summary>인형 마법 — 알리사·링 강화 (영혼 공명, 감정 전달, 자율 판단). 인형과 연결되고 싶음.</summary>
		Doll = 0,

		/// <summary>세계 탐구 — 안개 성질, 수정 동굴 빛, 시간 멈춘 이유. 세계 = 욘 내면임을 점진적 발견.</summary>
		World = 1,

		/// <summary>욘 자신 — "귀찮음의 원인", "내가 좋아하는 것", "인형을 만든 이유". 자기 이해 시도.</summary>
		Self = 2,
	}
}
