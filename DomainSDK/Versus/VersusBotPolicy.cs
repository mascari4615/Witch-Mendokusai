using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 연습 상대의 머리 (TASK-WM-411) — <b>판을 보고 입력을 만든다</b>. 여기서 나오는 것은
	/// 사람이 보내는 것과 똑같은 <see cref="VersusInputFrame"/> 이라, 판정은 누가 사람인지 모른다.
	///
	/// 엔진·네트워크를 모르는 순수 코드라 서버(빈자리 채우기)·유니티(혼자 연습)·시뮬(밸런스 측정)이 같이 쓴다.
	/// 강함이 목적이 아니다 — 사거리 유지 · 옆돌기 · 사거리 안이면 사격 · 탄이 가까우면 대시, 넷뿐이라 사람이 읽을 수 있다.
	/// </summary>
	public sealed class VersusBotPolicy
	{
		private readonly VersusBotTuning tuning;
		private readonly float halfWidth;
		private readonly float halfDepth;
		private float strafeSign;
		private float strafeTimer;

		public VersusBotPolicy(VersusBotTuning tuning, float halfWidth, float halfDepth, float initialStrafeSign, float initialStrafeTimer)
		{
			this.tuning = tuning;
			this.halfWidth = halfWidth;
			this.halfDepth = halfDepth;
			strafeSign = initialStrafeSign;
			strafeTimer = initialStrafeTimer;
		}

		/// <summary> 한 틱 생각한다. <paramref name="aimJitterRadians"/> 로 조준을 흔들면 사람에 가까워진다(0 = 완벽 조준). </summary>
		public VersusInputFrame Decide(VersusRoundState state, int selfIndex, float deltaTime, float aimJitterRadians)
		{
			int opponentIndex = 1 - selfIndex;
			Vector2 self = state.PositionOf(selfIndex);
			Vector2 opponent = state.PositionOf(opponentIndex);

			Vector2 toOpponent = new Vector2(opponent.x - self.x, opponent.y - self.y);
			float distance = Mathf.Sqrt(toOpponent.x * toOpponent.x + toOpponent.y * toOpponent.y);
			Vector2 forward = distance > 0.001f ? new Vector2(toOpponent.x / distance, toOpponent.y / distance) : new Vector2(1f, 0f);
			Vector2 side = new Vector2(-forward.y, forward.x);

			// 옆돌기 방향을 주기적으로 뒤집는다 — 한 방향으로만 돌면 사람이 한 번 읽고 끝난다.
			strafeTimer -= deltaTime;
			if (strafeTimer <= 0f)
			{
				strafeTimer = tuning.StrafeFlipSeconds;
				strafeSign = -strafeSign;
			}

			float approach = 0f;
			if (distance > tuning.PreferredDistance + tuning.DistanceTolerance)
				approach = 1f;
			else if (distance < tuning.PreferredDistance - tuning.DistanceTolerance)
				approach = -1f;

			Vector2 move = new Vector2(
				forward.x * approach + side.x * strafeSign,
				forward.y * approach + side.y * strafeSign);

			// 벽에 붙어 비비지 않게 안쪽으로 민다.
			if (Mathf.Abs(self.x) > halfWidth - tuning.WallMargin)
				move.x -= self.x >= 0f ? 1f : -1f;
			if (Mathf.Abs(self.y) > halfDepth - tuning.WallMargin)
				move.y -= self.y >= 0f ? 1f : -1f;

			float length = Mathf.Sqrt(move.x * move.x + move.y * move.y);
			if (length > 0.001f)
				move = new Vector2(move.x / length, move.y / length);
			else
				move = new Vector2(0f, 0f);

			Vector2 aim = aimJitterRadians == 0f ? forward : Rotate(forward, aimJitterRadians);

			return new VersusInputFrame
			{
				Move = move,
				Aim = aim,
				Fire = distance < tuning.MaxFireDistance,
				Dash = IsShotNear(state, selfIndex),
			};
		}

		// 탄이 코앞이면 대시. 봇이 「무엇을 보고 피했나」가 이 한 줄로 설명된다.
		private bool IsShotNear(VersusRoundState state, int selfIndex)
		{
			return state.HasIncomingShot(state.PositionOf(selfIndex), selfIndex, tuning.DodgeRadius);
		}

		private static Vector2 Rotate(Vector2 value, float radians)
		{
			float cos = Mathf.Cos(radians);
			float sin = Mathf.Sin(radians);
			return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
		}
	}
}
