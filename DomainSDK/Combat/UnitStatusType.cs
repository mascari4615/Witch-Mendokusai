namespace WitchMendokusai
{
	// 유닛 하나에 걸리는 상태이상 종류
	public enum UnitStatusType
	{
		Burn = 0,
		Poison = 1,
		Freeze = 2,
		Slow = 3,
		Stun = 4,
		Bleed = 5,
		Shock = 6,
		Mark = 7,
		Fear = 8,
		Silence = 9,
		// 보스 공격이 주인공에 쌓는 낙인. 문턱 (설정의 쌓임 상한) 에 닿으면 기절. 정본 memo wm/design/boss-arena-20261126
		Brand = 10,
	}
}
