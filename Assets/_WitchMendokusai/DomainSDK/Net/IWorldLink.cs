namespace WitchMendokusai.Net
{
	/// <summary>
	/// 세계로 이어진 줄 — <b>어디에 이어졌는지는 게임이 몰라도 된다</b> (TASK-WM-217).
	///
	/// 「혼자 하기」와 「같이 하기」를 나누지 않기 위한 자리다.
	/// 같은 줄이 어떤 때는 내 안의 세계에, 어떤 때는 멀리 있는 세계에 닿는다.
	/// 게임 쪽 코드는 그 차이를 묻지 않는다 — 그래서 모드가 하나로 합쳐진다.
	/// </summary>
	public interface IWorldLink
	{
		/// <summary>세계가 준 내 인형 번호. 아직이면 0.</summary>
		int MyDollId { get; }

		/// <summary>세계에 붙어 있나.</summary>
		bool IsLinked { get; }

		/// <summary>세계가 아는 <b>나</b>(신원 번호). 아직 모르면 0 — 인형 번호와 다르다(그건 접속마다 새로 준다).</summary>
		int MyIdentityId { get; }

		/// <summary>지금 보이는 인형들.</summary>
		WorldDollView[] Dolls { get; }

		/// <summary>지금 서 있는 건물들.</summary>
		BuildingView[] Buildings { get; }

		/// <summary>세계의 시각 — 아직 못 받았으면 null(그동안은 게임이 자기 시계를 쓴다).</summary>
		WorldTimeView Time { get; }

		/// <summary>이쪽으로 가고 싶다 — 얼마나 갈지는 세계가 정한다.</summary>
		void RequestMove(float x, float z);

		/// <summary>
		/// 여기에 짓고 싶다 — 겹치는지도, <b>몇 칸짜리인지도</b> 세계가 본다 (TASK-WM-217).
		/// 크기를 창이 보내던 길은 없앴다(「이건 1×1 이다」로 남의 집에 겹쳐 짓는 자리였다).
		/// </summary>
		void RequestPlace(int cellX, int cellY, int cellZ, int buildingId);

		/// <summary>이 칸의 건물을 부수고 싶다 — 정말 사라질지는 세계가 정한다.</summary>
		void RequestRemove(int cellX, int cellY, int cellZ);

		/// <summary>지금 솥의 모습 — 아직 못 받았으면 null.</summary>
		WorldBrewView Brew { get; }

		/// <summary>
		/// 이 재료를 솥에 넣는다 (TASK-WM-217) — <b>가방에서 실제로 빠진다</b>.
		/// 어느 쪽으로 얼마나 미는지는 세계가 재료에서 읽는다(창이 방향을 우기던 길은 없앴다).
		/// </summary>
		void RequestBrewStep(int itemId);

		/// <summary>솥을 비운다.</summary>
		void RequestBrewReset();

		/// <summary>이 솥을 완성으로 가져가겠다 — 줄지 말지는 세계가 정한다(선착순 한 번).</summary>
		void RequestBrewComplete();

		/// <summary>세계가 나에게 내준 완성. 아직 없으면 null. 한 번 읽으면 비운다.</summary>
		WorldBrewView TakeCompletedBrew();

		/// <summary>지금 세계에 서 있는 주울 것들 — 뽑아 간 자리는 빠져 있다.</summary>
		GatherableView[] Gatherables { get; }

		/// <summary>
		/// 저기 있는 저것을 줍겠다 (TASK-WM-217) — <b>손이 닿는지는 세계가 본다</b>.
		/// 「무엇을 몇 개 주웠다」고 창이 신고하던 길은 없앴다(그건 판정이 아니었다).
		/// </summary>
		void RequestGather(int nodeId);

		/// <summary>이걸 썼다 — 정말 있었는지는 세계가 본다(없으면 아무 일도 안 일어난다).</summary>
		void RequestConsume(int itemId, int amount);
	}
}
