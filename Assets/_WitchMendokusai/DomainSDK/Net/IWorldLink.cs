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

		/// <summary>지금 보이는 인형들.</summary>
		WorldDollView[] Dolls { get; }

		/// <summary>지금 서 있는 건물들.</summary>
		BuildingView[] Buildings { get; }

		/// <summary>세계의 시각 — 아직 못 받았으면 null(그동안은 게임이 자기 시계를 쓴다).</summary>
		WorldTimeView Time { get; }

		/// <summary>이쪽으로 가고 싶다 — 얼마나 갈지는 세계가 정한다.</summary>
		void RequestMove(float x, float z);

		/// <summary>여기에 짓고 싶다 — 겹치는지는 세계가 본다.</summary>
		void RequestPlace(int cellX, int cellY, int cellZ, int width, int length, int buildingId);

		/// <summary>이걸 줍고 싶다 — 가방에 들어갈지는 세계가 본다.</summary>
		void RequestGather(int itemId, int amount);
	}
}
