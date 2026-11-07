using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 세계가 걸음과 무관하게 옮긴 내 자리 (placed) 를 게임 창이 따르는 규칙 (2026-11-07).
	///
	/// - 걸음 방향: 세계가 아는 자리에서 화면 쪽 (<see cref="MoveIntent"/>)
	/// - 4m 미만 옮김: 화면은 옛 자리에 남고 걸음이 끌어당김 (<see cref="PositionCorrection"/>) 보다 빨라 옛 자리로 되돌아감
	/// - placed 수신 시: 화면을 그 자리로, 세계 목록이 그 자리를 보일 때까지 걸음 중지
	/// - 중지 이유: 목록이 늦는 동안 옛 자리 기준 걸음은 세계 자리를 지나침
	/// </summary>
	public sealed class PlacedFollow
	{
		/// <summary>세계 목록이 새 자리를 안 보여도 이만큼 판이 지나면 평소 걸음으로 복귀</summary>
		public const int MOST_HOLD_TICKS = 10;

		private bool holding;
		private Vector3 spot;
		private int heldTicks;

		public bool IsHolding => holding;

		public void Receive(float x, float z)
		{
			holding = true;
			spot = new Vector3(x, 0f, z);
			heldTicks = 0;
		}

		public void Clear()
		{
			holding = false;
			heldTicks = 0;
		}

		/// <summary>
		/// 이번 판에 화면을 옮길 자리. true 면 걸음과 끌어당김 생략
		/// 세계 목록이 새 자리를 보이거나 <see cref="MOST_HOLD_TICKS"/> 판이 지나면 다음 판부터 평소대로.
		/// </summary>
		public bool TryHold(Vector3 screen, Vector3 known, out Vector3 screenTo)
		{
			if (holding == false)
			{
				screenTo = screen;
				return false;
			}

			screenTo = new Vector3(spot.x, screen.y, spot.z);
			heldTicks++;
			Vector3 gap = new Vector3(known.x - spot.x, 0f, known.z - spot.z);
			if (gap.magnitude <= PositionCorrection.IGNORE_DISTANCE || heldTicks >= MOST_HOLD_TICKS)
				holding = false;

			return true;
		}
	}
}
