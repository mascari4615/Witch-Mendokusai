namespace WitchMendokusai
{
	/// <summary>
	/// 판(매치)을 그릴 때 <b>이름으로 찾는</b> 셰이더들 — 여기 적힌 것은 *전부* 그래픽 설정의
	/// 「항상 포함할 셰이더」에 등록돼 있어야 한다.
	///
	/// ★ 왜 한 곳에 모으나 (TASK-WM-208): 에디터는 프로젝트의 모든 셰이더를 들고 있어서
	///   `Shader.Find` 가 늘 성공한다. 빌드는 *쓰인다고 판단된 것*만 챙기므로, 이름으로만 찾는
	///   셰이더는 통째로 빠지고 **판이 회색이 된다**(사용자 실증 2026-08-03: "개척 진입하니까
	///   맵에 회색 밖에 안 보이는데"). 이름을 코드 한 곳에 모아둬야 에디터 시험이 설정과 대조할 수
	///   있고, 그게 33분짜리 빌드를 굽지 않고 이 병을 잡는 유일한 길이다.
	///
	/// 개척이 먼저 이 방식을 세웠고(`TowerDefenseShaderNames`), 투기장은 맨 `CreatePrimitive` 를 써서
	/// 같은 병을 안 고친 쪽이었다. 이제 두 게임이 **같은 목록**을 본다 — 개척 쪽은 여기로 포워딩한다.
	/// </summary>
	public static class CombatShaderNames
	{
		public const string Lit = "Universal Render Pipeline/Lit";
		public const string Unlit = "Universal Render Pipeline/Unlit";

		/// <summary> 정말 아무것도 없을 때의 마지막 수단 — 보이기는 한다. </summary>
		public const string LegacyFallback = "Sprites/Default";

		/// <summary> 빌드에 반드시 실려야 하는 것들(게임별 추가분은 각 게임 목록이 더한다). </summary>
		public static readonly string[] MustBeIncluded = { Lit, Unlit };
	}
}
