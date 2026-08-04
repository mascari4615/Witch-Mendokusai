namespace WitchMendokusai
{
	/// <summary>
	/// 핫바 한 칸 — 「무엇을 세우는 칸인가」 (TASK-WM-194).
	///
	/// ★ 왜 만들었나: 칸 번호 → 종류 대응이 *고정 산술*로 두 곳(화면·입력)에 따로 박혀 있었다.
	///   포탑 수만 변해도 되던 시절엔 버텼지만, 연구로 하나씩 해금하기 시작하면 그 산술은 그날로
	///   깨진다 — 화면은 세 칸을 그리는데 입력은 여섯 칸으로 세는 식이다(그러면 「함정을 골랐는데
	///   전초기지가 지어진다」가 된다). 목록을 규칙층이 만들고 둘 다 *그대로 읽는다*.
	/// </summary>
	public readonly struct TowerDefenseSlot
	{
		public TowerDefenseSlot(TowerDefensePlaceableKind kind, int towerIndex = 0)
		{
			Kind = kind;
			TowerIndex = towerIndex;
		}

		public TowerDefensePlaceableKind Kind { get; }

		/// <summary> 포탑 칸일 때 그 종류 번호. 나머지 종류에선 뜻이 없다. </summary>
		public int TowerIndex { get; }
	}
}
