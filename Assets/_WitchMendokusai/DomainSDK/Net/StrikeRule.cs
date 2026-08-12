using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>때리기</b>를 세계가 심판하는 규칙 (TASK-WM-251) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 여기인가: 싸움에서 창이 우길 수 있는 것은 셋이다 — <b>얼마나 멀리서</b>,
	///   <b>얼마나 자주</b>, <b>누구를</b>. 셋 다 세계가 봐야 한다.
	///   걸음을 시계로 심판한 것(MoveAllowance, TASK-WM-222)과 같은 자리다:
	///   횟수를 안 보면 창을 고쳐 초당 100번 때린다.
	///
	/// ★ 이건 <b>균형</b>이 아니라 <b>뼈대</b>다. 피해량·간격 숫자는 나중에 게임이 정한다 —
	///   여기서는 「세계가 판정한다」는 구조가 서는 것이 목적이다.
	/// </summary>
	public static class StrikeRule
	{
		/// <summary>때릴 수 있는 거리 (m) — 이보다 멀면 손이 안 닿는다.</summary>
		public const float REACH = 2f;

		/// <summary>때리고 다시 때리기까지 (ms).</summary>
		public const long COOLDOWN_MS = 600;

		/// <summary>한 대의 값.</summary>
		public const int DAMAGE = 10;

		/// <summary>가득 찬 몸.</summary>
		public const int FULL_HEALTH = 100;

		/// <summary>왜 못 때렸나.</summary>
		public enum Denial
		{
			None = 0,

			/// <summary>자기 자신은 못 때린다.</summary>
			Myself,

			/// <summary>그런 사람이 없다.</summary>
			NoSuchOne,

			/// <summary>손이 안 닿는다.</summary>
			TooFar,

			/// <summary>아직 팔이 안 돌아왔다.</summary>
			TooSoon,

			/// <summary>이미 쓰러진 사람은 안 때린다.</summary>
			AlreadyDown,
		}

		/// <summary>지금 이 사람을 때릴 수 있나.</summary>
		public static Denial CanStrike(int attackerId, int targetId, bool targetExists,
			Vector3 from, Vector3 to, int targetHealth, long lastStruckMs, long nowMs)
		{
			if (attackerId == targetId)
				return Denial.Myself;

			if (targetExists == false)
				return Denial.NoSuchOne;

			if (targetHealth <= 0)
				return Denial.AlreadyDown;

			if (nowMs - lastStruckMs < COOLDOWN_MS)
				return Denial.TooSoon;

			float awayX = to.x - from.x;
			float awayZ = to.z - from.z;
			if ((awayX * awayX) + (awayZ * awayZ) > REACH * REACH)
				return Denial.TooFar;

			return Denial.None;
		}

		/// <summary>맞고 나서 남는 몸 — 0 아래로는 안 내려간다.</summary>
		public static int HealthAfterHit(int health)
		{
			int left = health - DAMAGE;
			return left < 0 ? 0 : left;
		}
	}
}
