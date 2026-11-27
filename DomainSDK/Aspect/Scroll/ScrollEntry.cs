namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>두루마리에 든 것의 종류. 퍼즐 종류 (종이, 번갈아) 와 매이지 않음</summary>
	public enum ScrollResultKind
	{
		/// <summary>하찮은 마법. 마법 창에 들어가 원할 때 씀</summary>
		Spell = 0,

		/// <summary>정해진 제작대에서 만드는 법</summary>
		Recipe = 1,
	}

	/// <summary>
	/// 두루마리 안에 들 수 있는 것 하나. 처음 풀면 배우고, 또 나오면 다른 모습이 하나씩 열림.
	/// Target 은 게임 쪽 번호 (마법 번호나 레시피를 가진 아이템 번호). 규칙은 뜻을 모름
	/// </summary>
	public sealed class ScrollEntry
	{
		public int Id { get; set; }
		public ScrollResultKind Kind { get; set; }
		public int Target { get; set; }

		/// <summary>배운 것까지 합친 모습 수. 1 이면 다른 모습 없음</summary>
		public int Forms { get; set; } = 1;
	}
}
