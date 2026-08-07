namespace WitchMendokusai
{
	/// <summary>
	/// 조건·행동 종류가 <b>어떤 칸을 실제로 쓰는지</b>를 한 자리에 모은 것.
	///
	/// ★ 왜 별도 타입인가: 이 지식이 지금까지 세 곳에 흩어져 있었다 —
	///   ① <see cref="ConditionKind"/> 의 주석("수치 비교형은 Operator+Value, 불리언형은 Operator 무시")
	///   ② TacticConditions.EvalCondition 의 switch (실제로 읽는 곳)
	///   ③ 편집기 UI (어떤 칸을 보여줄지)
	///   주석은 강제력이 없고, ②와 ③이 어긋나면 <b>플레이어가 채운 값이 조용히 무시</b>되거나
	///   <b>채울 칸이 없어 기본값(Equal, 0)으로 굳는다.</b> 실측으로 후자였다 — 편집기가
	///   Operator/Value 칸을 안 만들어서 SelfHpRatio 룰이 전부 「HP 비율 == 0」(= 죽어야 참)이 되어
	///   영영 발동하지 않았다. 화면에선 그냥 「그 줄이 안 먹네」로만 보인다.
	///
	/// 그래서 ①을 코드로 내리고 ②·③이 여기를 보게 한다. 새 Kind 를 append 하면
	/// TacticSchemaTests 의 드리프트 검사가 <b>실제 평가 동작과 대조</b>해서 잡는다.
	/// (WM-165 가 예고한 「모드가 새 ConditionKind 를 등록」이 오면 이 자리가 계약이 된다.)
	/// </summary>
	public static class TacticSchema
	{
		/// <summary> 수치 비교형인가 — Operator + Value 를 실제로 읽는가. </summary>
		public static bool UsesThreshold(ConditionKind kind)
		{
			return kind switch
			{
				ConditionKind.SelfHp => true,
				ConditionKind.SelfHpRatio => true,
				ConditionKind.TargetHpRatio => true,
				ConditionKind.AllyCount => true,
				_ => false,
			};
		}

		/// <summary> 조건이 SkillSlot 을 읽는가. </summary>
		public static bool UsesSkillSlot(ConditionKind kind)
		{
			return kind == ConditionKind.SkillReady;
		}

		/// <summary> 행동이 SkillSlot 을 읽는가. </summary>
		public static bool UsesSkillSlot(ActionKind kind)
		{
			return kind == ActionKind.UseSkill;
		}
	}
}
