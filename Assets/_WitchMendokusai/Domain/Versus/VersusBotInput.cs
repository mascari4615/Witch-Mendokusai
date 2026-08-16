using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 연습 상대 (TASK-WM-411). 강한 AI 가 목적이 아니라 <b>혼자서도 손맛을 재 보는 것</b>이 목적이다
	/// — 친구가 자는 밤에도 「한 판 더」가 나오는지 절반은 확인할 수 있어야 한다.
	///
	/// 행동은 셋뿐이라 사람이 읽을 수 있다: ① 사거리를 유지하며 옆으로 돈다 ② 조준이 맞으면 쏜다
	/// ③ 탄이 가까이 오면 대시로 피한다. 예측·조준 리드는 일부러 안 넣었다 — 재미의 원인을 흐린다.
	/// </summary>
	public sealed class VersusBotInput : IVersusInput
	{
		private readonly Transform self;
		private readonly Transform opponent;
		private readonly VersusArena arena;
		private readonly VersusBotTuning tuning;

		private Vector2 move;
		private bool fireHeld;
		private bool dashPressed;
		private float strafeSign = 1f;
		private float strafeTimer;

		public VersusBotInput(Transform self, Transform opponent, VersusArena arena, VersusBotTuning tuning)
		{
			this.self = self;
			this.opponent = opponent;
			this.arena = arena;
			this.tuning = tuning;
		}

		public Vector2 ReadMove() => move;
		public bool IsFireHeld => fireHeld;
		public bool WasFirePressedThisFrame => fireHeld;
		public bool WasDashPressedThisFrame => dashPressed;

		public void Tick(float deltaTime)
		{
			dashPressed = false;

			if (self == null || opponent == null)
			{
				move = Vector2.zero;
				fireHeld = false;
				return;
			}

			Vector3 toOpponent = opponent.position - self.position;
			toOpponent.y = 0f;
			float distance = toOpponent.magnitude;
			Vector3 forward = distance > 0.001f ? toOpponent / distance : Vector3.right;
			Vector3 side = new Vector3(-forward.z, 0f, forward.x);

			// 옆돌기 방향을 주기적으로 뒤집는다 — 한 방향으로만 돌면 사람이 한 번 읽고 끝난다.
			strafeTimer -= deltaTime;
			if (strafeTimer <= 0f)
			{
				strafeTimer = tuning.StrafeFlipSeconds;
				strafeSign = -strafeSign;
			}

			// 너무 붙으면 물러나고 너무 멀면 다가간다 = 사거리 유지.
			float approach = 0f;
			if (distance > tuning.PreferredDistance + tuning.DistanceTolerance)
				approach = 1f;
			else if (distance < tuning.PreferredDistance - tuning.DistanceTolerance)
				approach = -1f;

			Vector3 desired = forward * approach + side * strafeSign;

			// 벽에 박혀 비비지 않게, 벽이 가까우면 안쪽으로 민다.
			Vector3 position = self.position;
			if (Mathf.Abs(position.x) > arena.HalfWidth - tuning.WallMargin)
				desired.x -= Mathf.Sign(position.x);
			if (Mathf.Abs(position.z) > arena.HalfDepth - tuning.WallMargin)
				desired.z -= Mathf.Sign(position.z);

			if (desired.sqrMagnitude > 0.001f)
				desired.Normalize();

			move = new Vector2(desired.x, desired.z);

			// 봇의 총구는 「이동 방향」이다(사람과 같은 규칙). 그래서 상대 쪽으로 갈 때만 맞는다 —
			// 이게 봇을 적당히 약하게 만들고, 사람 쪽 규칙과 어긋나지 않게 한다.
			float aimDot = desired.sqrMagnitude > 0.001f ? Vector3.Dot(desired.normalized, forward) : 0f;
			fireHeld = aimDot > tuning.FireAimDot && distance < tuning.MaxFireDistance;
		}

		/// <summary> 감독이 「탄이 코앞이다」를 알려 주면 대시로 피한다. 봇이 탄을 스스로 뒤지지 않게 한 것. </summary>
		public void NotifyIncoming()
		{
			dashPressed = true;
		}

		public void Dispose()
		{
		}
	}
}
