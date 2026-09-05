using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>한 번 훑었을 때 <b>새로 온 인형 / 떠난 인형</b> (TASK-WM-217).</summary>
	public readonly struct RosterChange
	{
		public RosterChange(IReadOnlyList<int> appeared, IReadOnlyList<int> left)
		{
			Appeared = appeared;
			Left = left;
		}

		/// <summary>이번에 처음 보인 인형들 — 몸을 세워야 한다.</summary>
		public IReadOnlyList<int> Appeared { get; }

		/// <summary>이번에 사라진 인형들 — 몸을 치워야 한다.</summary>
		public IReadOnlyList<int> Left { get; }
	}

	/// <summary>
	/// 세계가 보내온 인형 목록을 <b>「누가 왔고 누가 갔나」로 바꾸는 자리</b> (TASK-WM-217 단계 3).
	///
	/// ★ 왜 판정 층인가: 여기가 틀리면 화면에 <b>유령 몸</b>이 남거나 사람이 안 보인다.
	///   그건 눈으로 봐야만 알 수 있는 종류의 버그다 — 엔진 밖에서 시험할 수 있게 내려둔다.
	///   FishNet 이 스폰·디스폰으로 공짜로 주던 것이 정확히 이것이고, 이제 우리가 가진다.
	///
	/// <b>내 인형은 목록에서 뺀다</b> — 내가 조종하는 캐릭터가 이미 그 자리에 서 있어서,
	/// 대역을 하나 더 세우면 자기 몸에 겹쳐 보인다(FishNet 때 겪은 「메쉬 2개」와 같은 것).
	/// </summary>
	public sealed class DollRoster
	{
		private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
		private readonly HashSet<int> present = new HashSet<int>();
		private readonly List<int> appeared = new List<int>();
		private readonly List<int> left = new List<int>();

		/// <summary>지금 몸이 서 있어야 하는 인형 수 (내 것 제외).</summary>
		public int Count => positions.Count;

		/// <summary>세계가 보낸 목록을 받아 <b>차이</b>를 낸다. 돌려준 목록은 다음 훑기에서 다시 쓰인다.</summary>
		public RosterChange Sync(IReadOnlyList<WorldDollView> dolls, int myDollId)
		{
			appeared.Clear();
			left.Clear();
			present.Clear();

			if (dolls != null)
			{
				for (int i = 0; i < dolls.Count; i++)
				{
					WorldDollView view = dolls[i];
					if (view == null || view.id == myDollId)
						continue;

					// 같은 번호가 두 번 와도 몸은 하나 (서버가 겹쳐 보내도 화면은 안 깨진다).
					if (present.Add(view.id) == false)
						continue;

					Vector3 position = new Vector3(view.x, 0f, view.z);
					if (positions.ContainsKey(view.id) == false)
						appeared.Add(view.id);

					positions[view.id] = position;
				}
			}

			foreach (KeyValuePair<int, Vector3> known in positions)
			{
				if (present.Contains(known.Key) == false)
					left.Add(known.Key);
			}

			for (int i = 0; i < left.Count; i++)
				positions.Remove(left[i]);

			return new RosterChange(appeared, left);
		}

		/// <summary>그 인형이 지금 어디 있나.</summary>
		public bool TryGetPosition(int dollId, out Vector3 position) => positions.TryGetValue(dollId, out position);

		/// <summary>세계에서 나올 때 — 몸을 전부 치운다.</summary>
		public void Clear()
		{
			positions.Clear();
			present.Clear();
			appeared.Clear();
			left.Clear();
		}
	}

	/// <summary>
	/// 「가고 싶다」를 <b>세계가 받아 줄 크기의 걸음</b>으로 자르는 자리 (TASK-WM-217 단계 3).
	///
	/// 세계는 한 번에 <see cref="StepLimit.MOST_PER_STEP"/> 이상 못 가게 잘라낸다(순간이동 방지).
	/// 그보다 큰 걸음을 보내면 <b>조용히 잘려서</b> 캐릭터가 화면보다 뒤처진다 —
	/// 그래서 보내는 쪽이 먼저 같은 규칙으로 자르고, 남은 거리는 다음 걸음에 마저 보낸다.
	/// </summary>
	public static class MoveIntent
	{
		/// <summary>이만큼 안쪽이면 이미 도착 — 보내지 않는다(가만히 서 있는데 초당 20통 X).</summary>
		public const float ARRIVAL_EPSILON = 0.01f;

		/// <summary>지금 자리에서 원하는 자리로 가는 <b>한 걸음</b>. 이미 도착했으면 false.</summary>
		public static bool TryStep(Vector3 current, Vector3 desired, float maxStep, out Vector3 delta)
		{
			delta = new Vector3(desired.x - current.x, 0f, desired.z - current.z);
			if (delta.magnitude <= ARRIVAL_EPSILON)
			{
				delta = Vector3.zero;
				return false;
			}

			delta = Vector3.ClampMagnitude(delta, maxStep);
			return true;
		}
	}
}
