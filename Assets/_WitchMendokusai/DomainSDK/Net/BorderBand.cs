using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>국경 너머를 보는 규칙</b> (TASK-WM-263) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가: 세계를 나누면(WM-252) 국경에 선 사람은 <b>1m 옆</b>의 사람을 못 본다.
	///   저 사람은 옆 세계에 있고, 이 세계는 그를 모르기 때문이다. 그러면 한 세계가 아니라
	///   벽으로 갈린 두 게임이다 — 사람은 국경 근처를 「고장 난 자리」로 느낀다.
	///
	/// ★ 그래서 세계끼리 <b>국경 띠</b>를 서로 알려 준다: 이웃 땅에서 <see cref="BAND"/> 안에
	///   있는 내 사람들만 보낸다. 전부 보내면 그건 나누지 않은 것과 같다(나눈 이유가 회선이다).
	///
	/// ★ 넘어온 사람은 <b>그림자</b>다 — 보이기만 한다. 번호를 <b>음수</b>로 바꿔서
	///   이 세계의 번호와 절대 안 겹치게 한다(겹치면 남을 나로 그린다).
	/// </summary>
	public static class BorderBand
	{
		/// <summary>국경에서 이만큼 안쪽까지가 「띠」다 (m) — 보는 거리(관심 반경)와 같은 값.</summary>
		public const float BAND = 32f;

		/// <summary>한 세계가 이웃 하나에게 한 번에 보내는 사람 수 상한 — 띠가 붐벼도 회선을 안 먹는다.</summary>
		public const int MOST_SHADOWS = 48;

		/// <summary>그림자 번호의 자리 크기 — 세계 하나가 이만큼까지 번호를 쓴다고 본다.</summary>
		public const int ROOM_PER_ZONE = 1000000;

		/// <summary>
		/// 저 땅에서 이 자리까지의 거리 (m). 땅 안이면 0.
		/// 땅을 모르면(경계 없는 세계) 0 — 「어디든 이웃」이라 띠를 못 정한다.
		/// </summary>
		public static float AwayFrom(ZonePatch land, Vector3 spot)
		{
			if (land.Bounded == false)
				return 0f;

			Vector3 nearest = land.Clamp(spot);
			float deltaX = spot.x - nearest.x;
			float deltaZ = spot.z - nearest.z;
			return (float)System.Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
		}

		/// <summary>저 땅에 사는 사람에게 이 사람을 보여 줄까 — 국경 띠 안이면 그렇다.</summary>
		public static bool WorthTelling(ZonePatch neighbour, Vector3 spot)
		{
			return neighbour.Bounded && AwayFrom(neighbour, spot) <= BAND;
		}

		/// <summary>
		/// 세계 이름을 <b>작은 번호</b>로 — 같은 이름은 늘 같은 번호다(두 세계가 따로 세도 맞는다).
		/// 1..999 (0 은 「이름 없음」 자리로 비워 둔다).
		/// </summary>
		public static int MarkOfZone(string zoneName)
		{
			if (string.IsNullOrEmpty(zoneName))
				return 1;

			// 글자마다 굴린다 — 사전 없이도 두 세계가 같은 값을 낸다.
			int rolled = 17;
			foreach (char one in zoneName)
				rolled = ((rolled * 31) + one) & 0x7FFFFFF;

			return (rolled % 999) + 1;
		}

		/// <summary>
		/// 옆 세계 사람의 번호를 <b>이 세계에서 쓸 번호</b>로 바꾼다 — 늘 음수다.
		/// 음수라서 이 세계의 인형과 절대 안 겹치고, 창은 「못 건드리는 사람」으로 그린다.
		/// </summary>
		public static int ShadowId(string zoneName, int dollId)
		{
			if (dollId <= 0)
				return 0;

			int room = dollId % ROOM_PER_ZONE;
			return -((MarkOfZone(zoneName) * ROOM_PER_ZONE) + room);
		}

		/// <summary>이 번호가 옆 세계 사람인가.</summary>
		public static bool IsShadow(int dollId) => dollId < 0;
	}
}
