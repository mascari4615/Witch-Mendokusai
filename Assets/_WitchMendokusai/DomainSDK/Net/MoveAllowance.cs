using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>걸어서 갈 수 있는 만큼만</b> — 세계가 시계를 보고 걸음을 심판한다 (TASK-WM-222).
	///
	/// ★ 무엇이 빠져 있었나: 세계는 한 <b>번</b>의 걸음을 <see cref="WorldSim.MAX_STEP"/>(1.5m)로 잘랐다.
	///   자르는 것은 「한 번에 얼마나」뿐이고 「1초에 몇 번」은 아무도 안 봤다.
	///   그래서 창이 1초에 1000번 보내면 1초에 1500m 를 간다 — 자르는 문이 열려 있는 것과 같다.
	///   정상 창은 50ms 마다 0.15m 씩 보내므로, 이 구멍은 <b>속인 창에게만</b> 쓸모가 있었다.
	///
	/// ★ 왜 시계인가: 걸음은 거리 문제가 아니라 <b>속도</b> 문제다. 속도 = 거리 ÷ 시간이니
	///   시간을 안 보는 심판은 원리상 못 막는다. 그래서 사람마다 <b>걸음 지갑</b>을 둔다 —
	///   흐른 시간만큼 채워지고, 걸은 만큼 빠진다. 지갑이 비면 더는 못 간다.
	///
	/// ★ 왜 지갑(한 번에 다 안 잘라내기)인가: 인터넷은 고르지 않다. 200ms 밀렸다가 한꺼번에
	///   도착하는 것은 <b>정상</b>이다. 그걸 잘라 버리면 회선 나쁜 사람이 느려진다(속인 게 아닌데).
	///   그래서 잠깐 몰아 쓰는 것은 허락하되(지갑 한도 = 한 걸음), 평균 속도는 못 넘게 한다.
	///
	/// 판정은 엔진 밖이다 — 서버가 정본으로 쓰고, 유니티도 같은 답을 낸다.
	/// </summary>
	public sealed class MoveAllowance
	{
		/// <summary>창이 설계상 내는 걸음 속도 (m/s) — 웹 창의 LOCAL_MOVE_SPEED 와 같은 값.</summary>
		public const float WALK_SPEED = 3f;

		/// <summary>
		/// 여유. 창의 프레임·시계는 서버와 정확히 안 맞는다 — 딱 맞게 조이면 정상 창이 자꾸 걸린다.
		/// 속이는 쪽이 얻는 이득은 이 배수까지로 묶인다(1000배가 1.25배가 된다).
		/// </summary>
		public const float SPEED_TOLERANCE = 1.25f;

		/// <summary>세계가 실제로 허락하는 최고 속도 (m/s).</summary>
		public const float ALLOWED_SPEED = WALK_SPEED * SPEED_TOLERANCE;

		/// <summary>지갑에 모아 둘 수 있는 최대 거리 (m) — 밀렸다 몰려온 걸음을 받아 주는 폭.</summary>
		public const float BURST_DISTANCE = WorldSim.MAX_STEP;

		private readonly object gate = new object();
		private readonly Dictionary<int, Purse> purses = new Dictionary<int, Purse>();

		private struct Purse
		{
			public long LastMs;
			public float Meters;
		}

		/// <summary>
		/// <paramref name="wanted"/> 중 <b>지금 갈 수 있는 만큼</b>만 돌려준다.
		/// 남는 것은 버린다 — 빚으로 쌓아 두면 나중에 한꺼번에 순간이동이 된다.
		/// </summary>
		public Vector3 Allow(int dollId, long nowMs, Vector3 wanted)
		{
			float asked = new Vector3(wanted.x, 0f, wanted.z).magnitude;
			if (asked <= 0f)
				return Vector3.zero;

			lock (gate)
			{
				if (purses.TryGetValue(dollId, out Purse purse) == false)
				{
					// 처음 온 사람 = 지갑이 차 있다. 들어오자마자 한 걸음은 정상이다.
					purse = new Purse { LastMs = nowMs, Meters = BURST_DISTANCE };
				}
				else
				{
					// 시계가 뒤로 갔으면 흐른 시간은 0 — 되감아 채우는 구멍을 안 만든다.
					long elapsed = nowMs - purse.LastMs;
					if (elapsed > 0)
					{
						purse.Meters += ALLOWED_SPEED * (elapsed / 1000f);
						if (purse.Meters > BURST_DISTANCE)
							purse.Meters = BURST_DISTANCE;
					}

					purse.LastMs = nowMs;
				}

				float granted = asked <= purse.Meters ? asked : purse.Meters;
				purse.Meters -= granted;
				purses[dollId] = purse;

				if (granted <= 0f)
					return Vector3.zero;

				if (granted >= asked)
					return new Vector3(wanted.x, 0f, wanted.z);

				float scale = granted / asked;
				return new Vector3(wanted.x * scale, 0f, wanted.z * scale);
			}
		}

		/// <summary>나간 사람의 지갑을 버린다 — 안 버리면 사람 수만큼 영영 쌓인다.</summary>
		public void Forget(int dollId)
		{
			lock (gate)
			{
				purses.Remove(dollId);
			}
		}

		/// <summary>지금 들고 있는 지갑 수 — 새는지 보는 창구.</summary>
		public int Count
		{
			get
			{
				lock (gate)
				{
					return purses.Count;
				}
			}
		}
	}
}
