using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 내 화면의 나와 <b>세계가 아는 나</b>가 벌어졌을 때 어떻게 할까 (TASK-WM-217).
	///
	/// ★ 서버 권위의 빠진 반쪽: 세계는 한 걸음을 <see cref="StepLimit.MOST_PER_STEP"/> 으로 자른다.
	///   내가 그보다 빨리 달리면 <b>내 화면의 나만 앞서가고</b> 남들 화면의 나는 뒤처진다 —
	///   나는 문 앞에 서 있는데 남에게는 아직 복도에 있는 상태가 된다(그 상태로 문을 열면 남에겐 유령).
	///
	/// 그래서: 조금 벌어지면 <b>슬쩍 당기고</b>(사람은 못 느낀다), 많이 벌어지면 <b>바로 옮긴다</b>
	/// (그건 이미 「다른 곳에 있는 것」이라 부드럽게 끌면 오히려 오래 어긋난다).
	/// </summary>
	public static class PositionCorrection
	{
		/// <summary>이 안쪽이면 그냥 둔다 — 20Hz 스냅샷 사이의 정상적인 차이다.</summary>
		public const float IGNORE_DISTANCE = 0.35f;

		/// <summary>이보다 멀면 슬쩍 끄는 대신 바로 옮긴다.</summary>
		public const float TELEPORT_DISTANCE = 4f;

		/// <summary>1초에 벌어진 거리의 몇 할을 당길까(슬쩍 당기기 세기).</summary>
		public const float PULL_PER_SECOND = 3f;

		/// <summary>무엇을 할지.</summary>
		public enum Action
		{
			/// <summary>그냥 둔다.</summary>
			Keep,

			/// <summary>슬쩍 당긴다(사람은 못 느낀다).</summary>
			Pull,

			/// <summary>바로 옮긴다(이미 다른 곳에 있다).</summary>
			Snap,
		}

		/// <summary>
		/// 지금 내 자리와 세계가 아는 자리를 견줘 <b>다음에 있어야 할 자리</b>를 정한다.
		/// 높이(y)는 건드리지 않는다 — 그건 세계가 모르는 값이다(지형·점프는 각 창의 것).
		/// </summary>
		public static Action Resolve(Vector3 local, Vector3 world, float deltaTime, out Vector3 corrected)
		{
			Vector3 gap = new Vector3(world.x - local.x, 0f, world.z - local.z);
			float distance = gap.magnitude;

			if (distance <= IGNORE_DISTANCE)
			{
				corrected = local;
				return Action.Keep;
			}

			if (distance >= TELEPORT_DISTANCE)
			{
				corrected = new Vector3(world.x, local.y, world.z);
				return Action.Snap;
			}

			float pull = PULL_PER_SECOND * deltaTime;
			if (pull > 1f)
				pull = 1f;

			corrected = new Vector3(local.x + gap.x * pull, local.y, local.z + gap.z * pull);
			return Action.Pull;
		}
	}
}
