namespace WitchMendokusai
{
	/// <summary> DamagingObject 가 피격 대상을 분류하는 종류(레거시 2진 적아판정용). </summary>
	public enum VictimKind
	{
		Other = 0,
		Monster = 1,
		ResourceNode = 2,
		Player = 3,
	}

	/// <summary>
	/// 적아(데미지 여부) 판정 — 순수 함수(DomainSDK, MonoBehaviour 의존 0 → EditMode 직접 테스트).
	/// 매치(투기장·개척 등 — 공격자·피격자 둘 다 매치 참가자)면 팀 비교(다대다), 아니면 기존 usedByPlayer 2진 폴백.
	/// 레거시 분기는 DamagingObject 원본 switch 와 바이트 동등(도시/던전 PvE 회귀 0).
	/// </summary>
	public static class CombatRules
	{
		public static bool ShouldDamage(
			bool ownerInMatch, int ownerTeamId,
			bool victimInMatch, int victimTeamId,
			bool usedByPlayer, VictimKind victimKind)
		{
			// 매치 경로: 양쪽 다 매치 참가자면 팀 비교(다른 팀 = 적대).
			if (ownerInMatch && victimInMatch)
				return ownerTeamId != victimTeamId;

			// 레거시 폴백: 기존 2진 판정 그대로(MonsterObject/ResourceNode when usedByPlayer / Player when !usedByPlayer).
			return victimKind switch
			{
				VictimKind.Monster => usedByPlayer,
				VictimKind.ResourceNode => usedByPlayer,
				VictimKind.Player => usedByPlayer == false,
				_ => false,
			};
		}
	}
}
