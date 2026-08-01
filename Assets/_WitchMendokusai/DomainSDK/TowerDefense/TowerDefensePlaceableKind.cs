namespace WitchMendokusai
{
	/// <summary>
	/// 개척에서 설치할 수 있는 것의 종류 — 핫바 슬롯 1개당 하나 (TASK-WM-194).
	///
	/// 좌클릭=포탑 / 우클릭=채집 처럼 *버튼에 종류를 박는* 방식은 종류가 3개를 넘는 순간 못 늘린다
	/// (사용자 지시: "좌클릭/우클릭이 아니라 빌딩 핫바 좀 활용해야 할듯").
	/// 선택은 핫바가, 설치는 클릭이 맡는 문법이라 종류가 늘어도 슬롯만 늘면 된다.
	/// 가챠로 방어 인형이 늘어나는 방향(game-in-game-hub.md)과도 정합.
	/// </summary>
	public enum TowerDefensePlaceableKind
	{
		Tower = 0,     // 방어 인형 — 사거리 내 적을 쏜다.
		Harvester = 1, // 채집 인형 — 자원 노드 위에만, 수입 증가.
	}
}
