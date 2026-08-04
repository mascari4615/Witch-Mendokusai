namespace WitchMendokusai
{
	/// <summary>
	/// 결말 배너에 찍히는 문구 — 화면 없이도 확인할 수 있게 떼어낸 순수 규칙 (TASK-WM-194).
	///
	/// ★ 왜 떼어냈나: 라이브에서 잡힌 결함이 *문구 하나*였는데(진 판에 「최고 기록 — 48초 버팀」만 뜸)
	///   그 문구가 화면 컴포넌트 안에 묻혀 있어 시험으로 못 잡았다. 판정이 화면 안에 있으면
	///   「무슨 일이 일어났는지 화면이 제대로 말하나」를 영영 자동으로 못 묻는다.
	/// </summary>
	public static class TowerDefenseOutcomeText
	{
		/// <summary>
		/// 배너 첫 줄 + 성적 줄. survived 는 이미 사람이 읽는 형태(「48초」)로 다듬어 넘긴다.
		///
		/// 규칙은 둘뿐이다: ① 이겼나 졌나를 *먼저* 또렷이 말한다 ② 성적·기록은 그 뒤에 붙인다.
		/// 기록 경신이 결과를 가리면 안 된다 — 져도 기록은 깨질 수 있다(실제로 그래서 사고가 났다).
		/// </summary>
		public static string Build(TowerDefenseOutcome outcome, string survived, int nestsDestroyed,
			int score, int best, bool isNewRecord)
		{
			string nests = nestsDestroyed > 0 ? "  ·  둥지 " + nestsDestroyed + "곳 부숨" : string.Empty;
			string scoreLine = "\n점수 " + score + (isNewRecord ? "  ·  이전 최고 " + best : "  ·  최고 " + best);

			if (outcome == TowerDefenseOutcome.Victory)
				return "개척 성공 — 마지막 둥지를 무너뜨렸다\n" + survived + nests + scoreLine;

			string record = isNewRecord ? "  ·  최고 기록 경신" : string.Empty;
			return "개척 실패 — " + survived + " 버팀" + record + nests + scoreLine;
		}
	}
}
