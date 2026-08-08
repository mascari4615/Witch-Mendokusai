namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-196 단계 7 — 「그 모드에 **막** 들어왔다 / **막** 나갔다」 그 순간만 알려 준다.
	///
	/// ★ 왜 필요한가: 모드 변경 알림은 같은 상태로도 다시 온다(씬이 처음 서면서 현재 모드를 한 번 더
	///   적용하는 식으로). 그때마다 입력 방식을 갈아끼우거나 판을 다시 시작하면, 이미 세팅해 둔 것과
	///   부딪히거나 판이 두 번 시작된다. 그래서 두 모드 컨트롤러가 각자 「직전에 이 모드였나」 깃발을
	///   손으로 들고 있었다 — 글자까지 같은 코드가 두 벌.
	///
	/// ★ 왜 상태도 같이 들고 있나: 개척 쪽은 그 깃발을 전이 판정뿐 아니라 **「지금 이 모드인가」**를
	///   묻는 데도 쓰고 있었다(세 곳). 전이만 뽑고 상태를 두고 오면 깃발이 두 개로 갈라져,
	///   둘이 어긋나는 날이 온다. 한 물건이 두 질문에 답하게 둔다.
	///
	/// 유니티에 안 붙는 순수 코드다 — EditMode 로 몇 초 만에 검증된다.
	/// </summary>
	public sealed class ModeControllerEdgeTrigger
	{
		/// <summary>지금 그 모드인가. 마지막으로 건네받은 상태를 그대로 답한다.</summary>
		public bool IsActive { get; private set; }

		/// <summary>
		/// 상태가 바뀐 순간이면 <c>true</c> (그리고 새 상태를 기억한다). 같은 상태면 <c>false</c> —
		/// 부르는 쪽은 그대로 돌아가면 된다.
		/// </summary>
		public bool Crossed(bool isActive)
		{
			if (isActive == IsActive)
			{
				return false;
			}

			IsActive = isActive;
			return true;
		}
	}
}
