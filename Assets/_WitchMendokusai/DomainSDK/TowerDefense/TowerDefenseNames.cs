namespace WitchMendokusai
{
	/// <summary>
	/// 인형에게 이름을 준다(TASK-WM-194).
	///
	/// ★ 왜 필요한가: 지금 화면에 서 있는 것은 「광역 포탑」이고, 그건 *물건*이다. 물건은 팔 때 아깝지 않고
	///   죽어도 아무 일이 아니다. 이름이 붙는 순간 같은 유닛이 **아이**가 되고, 판다·잃는다의 무게가 생긴다.
	///   개척은 마녀가 인형을 데리고 나가는 이야기라 이 층이 없으면 세계관이 화면에 하나도 안 나온다.
	///
	/// ★ 왜 결정적인가: 같은 판(씨앗)에서 같은 순서로 세우면 같은 이름이 붙는다 — 다시 봤을 때 다른 아이가
	///   되어 있으면 애착이 붙을 자리가 없다.
	///
	/// ⚠ 이름 목록은 기능 자리를 채운 임시안 — 세계관 정본 명명은 사용자 영역이다.
	/// 순수 정적 — 씬·RNG 0.
	/// </summary>
	public static class TowerDefenseNames
	{
		private static readonly string[] DollNames =
		{
			"비올라", "루비", "미르", "샤샤", "노을", "하루", "티나", "코코",
			"단이", "여울", "라라", "모리", "포포", "세라", "은결", "차차",
			"리리", "가온", "누리", "타비", "소미", "윤슬", "메이", "달래",
		};

		private static readonly string[] Greetings =
		{
			"여기 맡겨.",
			"잘 부탁해!",
			"어디 보자…",
			"놓치지 않아.",
			"준비 됐어.",
			"이 자리 좋네.",
		};

		/// <summary> 이번 판 ordinal 번째로 세운 인형의 이름. 같은 판·같은 순서면 같은 이름. </summary>
		public static string For(int seed, int ordinal)
		{
			int index = Hash(seed, ordinal) % DollNames.Length;
			return DollNames[index];
		}

		/// <summary> 세워질 때 한 마디 — 말이 있으면 물건이 아니라 아이가 된다. </summary>
		public static string Greeting(int seed, int ordinal)
		{
			int index = Hash(seed * 7919 + 13, ordinal) % Greetings.Length;
			return Greetings[index];
		}

		private static int Hash(int a, int b)
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 486187739 + a;
				hash = hash * 486187739 + b;
				hash ^= hash >> 13;
				return hash & 0x7fffffff;
			}
		}
	}
}
