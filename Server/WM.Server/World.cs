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

		// ★ 여러 갈래가 동시에 만진다 (TASK-WM-216): 접속·퇴장은 각 연결의 흐름에서, 훑기는 알림 루프에서.
		//   자물쇠 없이 두었더니 알림 루프가 훑는 도중 목록이 바뀌어 **터졌다**(NullReference).
		//   화면 없는 서버라 터져도 티가 안 난다 — 그래서 상태를 만지는 자리를 전부 한 자물쇠 아래 둔다.
		private readonly object gate = new object();
		private readonly Dictionary<int, Doll> dolls = new Dictionary<int, Doll>();
		private int nextId = 1;

		/// <summary>훑을 때는 <b>그 순간의 사본</b>을 준다 — 훑는 동안 목록이 바뀌어도 안전하다.</summary>
		public Doll[] Snapshot()
		{
			lock (gate)
			{
				Doll[] copy = new Doll[dolls.Count];
				dolls.Values.CopyTo(copy, 0);
				return copy;
			}
		}

		public Doll Join()
		{
			lock (gate)
			{
				Doll doll = new Doll(nextId++, Vector3.zero);
				dolls[doll.Id] = doll;
				return doll;
			}
		}

		public void Leave(int dollId)
		{
			lock (gate)
			{
				dolls.Remove(dollId);
			}
		}

		/// <summary>
		/// 움직임 요청을 <b>서버가 판정한다.</b> 클라가 보낸 값을 그대로 믿지 않는다 —
		/// 한 번에 갈 수 있는 거리로 잘라낸다(믿으면 순간이동이 공짜가 된다).
		/// </summary>
		public bool TryMove(int dollId, Vector3 delta)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out Doll doll) == false)
					return false;

				Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
				doll.Position = doll.Position + clamped;
				return true;
			}
		}
	}
}
