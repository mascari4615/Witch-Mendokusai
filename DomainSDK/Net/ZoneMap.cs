using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>옆 세계가 어디에 있나</b> (TASK-WM-254) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가: 세계를 나누면(WM-252) 경계에서 「이 자리는 누구 땅인가」를 알아야
	///   사람을 넘겨줄 수 있다. 모르면 경계에 세워 두는 수밖에 없다(그건 벽이지 국경이 아니다).
	///
	/// ★ 왜 판정 층인가: 이 지도는 <b>모든 세계가 똑같이</b> 알아야 한다. 한쪽만 다르게 알면
	///   사람이 두 세계에 동시에 있거나(가방 복사) 어느 쪽에도 없게 된다(사라짐).
	/// </summary>
	public sealed class ZoneMap
	{
		private readonly List<(ZonePatch Patch, string Address)> lands = new List<(ZonePatch, string)>();

		/// <summary>아는 이웃이 없다 — 안 나눈 세계는 이걸 쓴다.</summary>
		public static ZoneMap Alone => new ZoneMap();

		public int Count => lands.Count;

		public void Add(ZonePatch patch, string address)
		{
			if (patch.Bounded == false || string.IsNullOrEmpty(address))
				return;

			lands.Add((patch, address));
		}

		/// <summary>아는 이웃 전부 — 국경 띠를 서로 알려 줄 때 하나씩 돌아본다 (TASK-WM-263).</summary>
		public IReadOnlyList<(ZonePatch Patch, string Address)> Lands => lands;

		/// <summary>이 자리를 맡은 이웃 — 없으면 <c>false</c>.</summary>
		public bool TryOwner(Vector3 spot, out string name, out string address)
		{
			name = null;
			address = null;

			foreach ((ZonePatch Patch, string Address) land in lands)
			{
				if (land.Patch.Contains(spot) == false)
					continue;

				name = land.Patch.Name;
				address = land.Address;
				return true;
			}

			return false;
		}

		/// <summary>
		/// 「이름:fromX,fromZ,toX,toZ=주소」 를 <c>;</c> 로 이어 적은 것을 읽는다.
		/// 못 읽는 조각은 <b>건너뛴다</b> — 하나가 잘못 적혔다고 나머지 이웃까지 잃으면
		/// 그 경계는 통째로 벽이 된다.
		/// </summary>
		public static ZoneMap Read(string said)
		{
			ZoneMap map = new ZoneMap();
			if (string.IsNullOrEmpty(said))
				return map;

			foreach (string one in said.Split(';'))
			{
				if (string.IsNullOrEmpty(one))
					continue;

				int at = one.LastIndexOf('=');
				if (at <= 0 || at == one.Length - 1)
					continue;

				ZonePatch patch = ZonePatch.Read(one.Substring(0, at).Trim());
				if (patch.Bounded == false)
					continue;

				map.Add(patch, one.Substring(at + 1).Trim());
			}

			return map;
		}
	}
}
