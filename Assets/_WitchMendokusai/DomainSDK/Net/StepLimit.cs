namespace WitchMendokusai.Net
{
	/// <summary>
	/// 한 번에 갈 수 있는 거리 — <b>세계와 회선이 함께 쓰는 규칙</b> (TASK-WM-409 후속).
	///
	/// ★ 왜 여기 있나 — 이 값은 원래 <c>WorldSim.MAX_STEP</c> 이었다. 그런데
	///   <see cref="MoveAllowance"/>(회선 층)가 그걸 읽고, 세계는 거꾸로 <see cref="StrikeRule"/>·
	///   <see cref="LineTime"/>(회선 층)를 읽는다. 어셈블리를 가르는 순간 그건 <b>순환</b>이 되고,
	///   유니티는 순환을 아예 거부한다 — 실측 2026-08-17: 방치형 빌드가 이것 하나로 7판 연속 죽었다.
	///
	/// ★ 그래서 <b>양쪽이 다 보는 쪽</b>에 값을 둔다. 세계 → 회선은 이미 한 방향으로 열려 있으니,
	///   걸음 상한이 회선 쪽에 있으면 화살표가 하나로 정리된다.
	///   값은 여전히 <b>한 곳</b>이다 — <c>WorldSim.MAX_STEP</c> 은 이 값을 가리키는 이름일 뿐이라
	///   부르던 자리 14곳은 그대로 둔다(같은 수치를 두 곳에 박지 않는다).
	/// </summary>
	public static class StepLimit
	{
		/// <summary>한 번 움직임에 갈 수 있는 거리 상한 — 순간이동 방지(서버 권위의 최소선).</summary>
		public const float MOST_PER_STEP = 1.5f;
	}
}
