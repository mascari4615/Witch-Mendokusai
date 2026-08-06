using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 스폰 자리 검사 — 순수 함수(DomainSDK, MonoBehaviour 의존 0 → EditMode 직접 테스트).
	///
	/// ★ 왜 필요한가: 같은 자리에 둘을 세우면 물리가 서로를 밀어내는데, 캡슐 둘이 완전히 겹친 상태는
	///   밀어낼 방향이 없어서 값이 튄다. 실측으로 유닛이 맵 밖(1236,-2906,2015)까지 날아가 **죽지도
	///   않고 영영 살아남아** 매치가 안 끝난 적이 있다. 화면에선 「다 잡았는데 안 넘어간다」로만 보인다.
	///
	/// 맵은 데이터(ArenaMapSO)라 사람이 수치를 만진다 — 폭보다 여백(SpawnInset)을 크게 잡는 순간
	/// 스폰이 한 점으로 모인다. 그런 판은 **시작 전에 거절하는 게** 시작해놓고 이상하게 구는 것보다 낫다.
	/// </summary>
	public static class SpawnRules
	{
		/// <summary>
		/// 너무 가까이 붙은 스폰 쌍이 있으면 true 와 그 두 인덱스. 거리 비교는 제곱으로(루트 불요).
		/// minSeparation 이 0 이하면 검사하지 않는다(끄고 싶은 맵을 위한 탈출구).
		/// </summary>
		public static bool TryFindOverlap(IReadOnlyList<Vector3> spawns, float minSeparation, out int first, out int second)
		{
			first = -1;
			second = -1;

			if (spawns == null || minSeparation <= 0f)
				return false;

			float sqrMin = minSeparation * minSeparation;
			for (int i = 0; i < spawns.Count; i++)
			{
				for (int j = i + 1; j < spawns.Count; j++)
				{
					if ((spawns[i] - spawns[j]).sqrMagnitude < sqrMin)
					{
						first = i;
						second = j;
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// <b>서로 다른 두 팀</b>의 스폰이 겹치는지. <paramref name="first"/> 는 <paramref name="a"/> 의,
		/// <paramref name="second"/> 는 <paramref name="b"/> 의 인덱스다.
		///
		/// ★ 왜 팀 안 검사만으로 부족한가 (2026-08-06 실측): `RectangleArenaMap` 은 스폰 z 를
		///   `±(Length/2 - SpawnInset)` 로 잡는다. `SpawnInset` 이 `Length/2` 가 되면
		///   **두 팀이 똑같이 z=0** 에 서고 팀0 i번째와 팀1 i번째가 **정확히 같은 점**이 된다.
		///
		///   ★ 팀 단위 검사가 이걸 놓치는 건 <b>넓은 판</b>에서다(실측 계산):
		///     - 좁은 판(24×36, inset 18) → X 폭도 같이 0 이 되어 팀 *안* 이 먼저 붕괴 = 팀 검사가 잡는다.
		///     - **넓은 판(40×20, inset 10) → X 는 -10/0/+10 로 멀쩡한데 z 만 붕괴** = 팀 안은 아무 이상 없고
		///       팀끼리만 정확히 포개진다. 이 경우가 팀 단위 검사로는 통째로 안 보인다.
		///   즉 `Width > 2*SpawnInset` 이면서 `SpawnInset ≈ Length/2` 인 구간이 사각지대다.
		/// </summary>
		public static bool TryFindOverlapAcross(
			IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, float minSeparation, out int first, out int second)
		{
			first = -1;
			second = -1;

			if (a == null || b == null || minSeparation <= 0f)
				return false;

			float sqrMin = minSeparation * minSeparation;
			for (int i = 0; i < a.Count; i++)
			{
				for (int j = 0; j < b.Count; j++)
				{
					if ((a[i] - b[j]).sqrMagnitude < sqrMin)
					{
						first = i;
						second = j;
						return true;
					}
				}
			}

			return false;
		}
	}
}
