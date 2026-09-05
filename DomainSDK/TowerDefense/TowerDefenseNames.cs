namespace WitchMendokusai
{
	/// <summary>
	/// 인형을 개체로 구분하는 표식(TASK-WM-194).
	///
	/// ★ 왜 이름이 필요한가: 같은 색 같은 모양이 여럿 서 있으면 「아까 그 아이」를 가리킬 수단이 없다.
	///   개체 식별은 기능이다 — 팔 때·승급할 때·죽었을 때 무엇이 사라졌는지 알아야 한다.
	///
	/// ★ 왜 *임의로 지은 이름*이 아닌가 (사용자 지시): 세계관·설정·명명은 사용자 영역이다.
	///   프로토타입 단계에서 그럴듯한 이름을 미리 박으면, 나중에 진짜 이름이 정해질 때
	///   「이미 있는 것 같은 착각」과 충돌한다. 그래서 자리만 잡아두는 번호 표식을 쓴다.
	///   진짜 명명이 정해지면 이 파일의 두 함수만 갈아끼우면 된다.
	///
	/// 순수 정적 — 씬·RNG 0.
	/// </summary>
	public static class TowerDefenseNames
	{
		/// <summary> 이번 판 ordinal 번째로 세운 인형의 표식. 같은 판·같은 순서면 같은 표식. </summary>
		public static string For(int seed, int ordinal)
		{
			return "유닛이름" + (ordinal + 1);
		}

		/// <summary> 세워질 때 한 마디 — 자리만 잡아둔 것(문안은 사용자 영역). </summary>
		public static string Greeting(int seed, int ordinal)
		{
			return "대사" + (ordinal + 1);
		}
	}
}
