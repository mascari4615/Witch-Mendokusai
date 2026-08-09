using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>접속한 사람 하나 — 서버가 아는 것은 이만큼이다 (TASK-WM-216).</summary>
	public sealed class Doll
	{
		public Doll(int id, Vector3 position)
		{
			Id = id;
			Position = position;
		}

		public int Id { get; }
		public Vector3 Position { get; set; }
	}

	/// <summary>
	/// 서버가 굴리는 세계 — <b>판정만</b> 있고 화면은 없다 (TASK-WM-216).
	///
	/// 여기 있는 규칙은 게임과 같은 것을 쓴다(좌표·수학 = DomainSDK).
	/// 「어떻게 보이나」는 각 창(Unity · 웹)이 알아서 한다.
	/// </summary>
	public sealed class World
	{
		/// <summary>한 번 움직임에 갈 수 있는 거리 상한 — 순간이동 방지(서버 권위의 최소선).</summary>
		public const float MAX_STEP = 1.5f;

		private readonly Dictionary<int, Doll> dolls = new Dictionary<int, Doll>();
		private int nextId = 1;

		public IReadOnlyCollection<Doll> Dolls => dolls.Values;

		public Doll Join()
		{
			Doll doll = new Doll(nextId++, Vector3.zero);
			dolls[doll.Id] = doll;
			return doll;
		}

		public void Leave(int dollId) => dolls.Remove(dollId);

		/// <summary>
		/// 움직임 요청을 <b>서버가 판정한다.</b> 클라가 보낸 값을 그대로 믿지 않는다 —
		/// 한 번에 갈 수 있는 거리로 잘라낸다(믿으면 순간이동이 공짜가 된다).
		/// </summary>
		public bool TryMove(int dollId, Vector3 delta)
		{
			if (dolls.TryGetValue(dollId, out Doll doll) == false)
				return false;

			Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
			doll.Position = doll.Position + clamped;
			return true;
		}
	}
}
