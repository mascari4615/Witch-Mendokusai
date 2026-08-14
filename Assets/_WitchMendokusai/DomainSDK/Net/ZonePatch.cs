using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 한 세계가 <b>맡은 땅</b> (TASK-WM-252) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가 (실측 2026-08-12): 사람이 늘 때 먼저 막히는 것은 CPU 가 아니라 <b>회선</b>이었다
	///   (800명에 CPU 6%, 대역 65Mbps). 한 기계의 회선은 늘릴 수 없으니, 넘으려면 세계를 <b>나눠</b>
	///   각자 다른 기계가 맡아야 한다. 그 첫걸음은 「이 세계는 여기까지가 내 땅이다」를 아는 것이다.
	///
	/// ★ 왜 판정이 여기인가: 어느 땅이 누구 것인가는 <b>모든 세계가 똑같이</b> 알아야 한다.
	///   한쪽만 다르게 알면 사람이 두 세계에 동시에 있거나 어느 쪽에도 없게 된다.
	///
	/// 땅이 없으면(<see cref="Everywhere"/>) 온 세상이 내 것이다 — 안 나눈 세계는 지금 그대로 돈다.
	/// </summary>
	public readonly struct ZonePatch
	{
		public ZonePatch(string name, float fromX, float fromZ, float toX, float toZ)
		{
			Name = name ?? string.Empty;
			FromX = fromX < toX ? fromX : toX;
			FromZ = fromZ < toZ ? fromZ : toZ;
			ToX = fromX < toX ? toX : fromX;
			ToZ = fromZ < toZ ? toZ : fromZ;
			Bounded = true;
		}

		/// <summary>안 나눈 세계 — 온 세상이 자기 땅이다.</summary>
		public static ZonePatch Everywhere => default;

		public string Name { get; }

		public float FromX { get; }

		public float FromZ { get; }

		public float ToX { get; }

		public float ToZ { get; }

		/// <summary>땅이 정해져 있나 — <c>false</c> 면 온 세상이다.</summary>
		public bool Bounded { get; }

		/// <summary>이 자리가 내 땅인가.</summary>
		public bool Contains(Vector3 spot)
		{
			if (Bounded == false)
				return true;

			return spot.x >= FromX && spot.x <= ToX && spot.z >= FromZ && spot.z <= ToZ;
		}

		/// <summary>
		/// 이 땅과 저 땅이 <b>겹치나</b> (TASK-WM-368).
		///
		/// ★ 왜 봐야 하나: 겹친 자리는 <b>두 세계가 다 자기 땅이라고 우기는 자리</b>다.
		///   그 자리에 선 사람은 넘겨주기가 오락가락하며 두 세계를 왕복하거나, 최악에는 <b>둘로 늘어난다</b>.
		///   땅 설정은 손으로 적는 글자(WM_ZONE)라 오타 한 번이면 그렇게 된다.
		///   ⚠ <b>맞닿은 것</b>은 겹친 것이 아니다 — 국경선은 서로 나눠 갖는다(경계값은 양쪽이 다 가진다).
		/// </summary>
		public bool Overlaps(ZonePatch other)
		{
			if (Bounded == false || other.Bounded == false)
				return false;

			bool apartInX = ToX <= other.FromX || other.ToX <= FromX;
			bool apartInZ = ToZ <= other.FromZ || other.ToZ <= FromZ;
			return apartInX == false && apartInZ == false;
		}

		/// <summary>
		/// 내 땅 안으로 끌어당긴 자리 — 밖으로 나가려 하면 <b>경계에 세운다</b>.
		/// (넘겨주기가 서기 전까지는 이것이 정직하다: 남의 땅을 내가 굴리면 두 세계가 갈라진다.)
		/// </summary>
		public Vector3 Clamp(Vector3 spot)
		{
			if (Bounded == false)
				return spot;

			float x = spot.x < FromX ? FromX : (spot.x > ToX ? ToX : spot.x);
			float z = spot.z < FromZ ? FromZ : (spot.z > ToZ ? ToZ : spot.z);
			return new Vector3(x, spot.y, z);
		}

		/// <summary>경계에 바싹 붙었나 — 창에 「여기가 끝이다」를 알려 줄 때 쓴다.</summary>
		public bool AtEdge(Vector3 spot, float margin)
		{
			if (Bounded == false)
				return false;

			return spot.x - FromX <= margin || ToX - spot.x <= margin
				|| spot.z - FromZ <= margin || ToZ - spot.z <= margin;
		}

		/// <summary>
		/// 「이름:fromX,fromZ,toX,toZ」 로 적힌 땅을 읽는다 — 못 읽으면 온 세상.
		/// 세계를 띄울 때 환경에서 받는 모양이다.
		/// </summary>
		public static ZonePatch Read(string said)
		{
			if (string.IsNullOrEmpty(said))
				return Everywhere;

			string[] halves = said.Split(':');
			string name = halves.Length > 1 ? halves[0] : string.Empty;
			string[] numbers = (halves.Length > 1 ? halves[1] : halves[0]).Split(',');
			if (numbers.Length != 4)
				return Everywhere;

			float[] four = new float[4];
			for (int i = 0; i < 4; i++)
			{
				if (float.TryParse(numbers[i].Trim(), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out four[i]) == false)
				{
					return Everywhere;
				}
			}

			return new ZonePatch(name, four[0], four[1], four[2], four[3]);
		}
	}
}
