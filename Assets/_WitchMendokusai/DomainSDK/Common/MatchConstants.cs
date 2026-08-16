namespace WitchMendokusai
{
	/// <summary>
	/// 대결(Versus)·아레나(Arena)가 함께 쓰는 「승자 없음」 센티넬 — **한 곳에만 둔다**.
	/// 전에는 ArenaModeSO 와 VersusMatchCore 가 각자 -1 을 들고 있었다(2026-08-17 게이트가 잡음).
	/// 같은 뜻의 수를 두 곳에 두면 한쪽만 고쳐진다 — 그게 「어느 판에서만 무승부가 이상하다」의 뿌리다.
	/// </summary>
	public static class MatchConstants
	{
		/// <summary> 승자 없음(진행 중 또는 무승부). TeamId·PlayerIndex 어느 쪽으로도 쓴다. </summary>
		public const int NO_WINNER = -1;
	}
}
