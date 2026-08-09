using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 검증 스크립트도 게임과 같은 타입으로 말해야 한다.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 전초기지·바깥 채집 확인 도구(TASK-WM-194).
	///
	/// ★ 왜 따로 만드나: 이 둘은 *정수*와 *먼 거리*를 동시에 요구해서 일반 하네스의 예산으로는 영영
	///   못 세운다. 그래서 이 작업 내내 「규칙층은 확증됐지만 라이브는 미확인」으로 남아 있었다.
	///   확인하지 못한 것을 확인했다고 적지 않으려면, *확인할 수 있는 도구*를 만드는 수밖에 없다.
	/// ★ 왜 치트인가: 자원·정수를 채우는 것은 *배치 규칙을 우회하지 않는다* — 여전히 보급이 닿아야 하고,
	///   암반 위엔 못 서고, 칸이 비어야 한다. 값만 넉넉히 주고 **규칙은 그대로 통과시켜** 확인한다.
	///
	/// Play 중에만 쓸 수 있다.
	/// </summary>
	public static class TowerDefenseOutpostVerify
	{
		private const string TAG = "[TD-Outpost]";

		[MenuItem("WM/TowerDefense/Verify Outpost + Outer Harvester")]
		public static void Run()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning(TAG + " Play 중에만 쓸 수 있다.");
				return;
			}

			TowerDefenseMatch match = Object.FindAnyObjectByType<TowerDefenseMatch>();
			if (match == null || match.StageRoot == null)
			{
				Debug.LogError(TAG + " 매치 없음 — 개척 모드로 들어간 뒤 실행.");
				return;
			}

			// 값만 채운다 — 자리·보급·암반 규칙은 그대로 통과시켜야 확인이 의미가 있다.
			match.GrantForVerification(4000, 400);
			Debug.Log(TAG + " 값 지급 — 자원 " + match.Resource + " 정수 " + match.Essence);

			// 코어에서 바깥으로 뻗으며 전초기지를 세운다(보급이 닿는 데까지).
			Vector3 core = match.CoreCombatant != null ? match.CoreCombatant.Position : (Vector3)match.StageRoot.position;
			int outpostsBuilt = 0;
			for (int step = 1; step <= 6; step++)
			{
				Vector3 target = core + new Vector3(step * 8f, 0f, step * 4f);
				if (match.TryPlaceOutpost(target))
					outpostsBuilt++;
			}

			Debug.Log(TAG + (outpostsBuilt > 0
				? " OUTPOST-OK 전초기지 " + outpostsBuilt + "기 실제 배치 ✔"
				: " OUTPOST-FAIL 한 기도 못 세움 — 콘솔의 거절 사유 확인"));

			// 이제 바깥 노드에 채집을 세운다 — 전초기지가 새 보급 원점이 됐으므로 닿아야 한다.
			// ★ *먼* 노드부터 잡는다 — 가까운 것부터 잡으면 안쪽만 채워져 「바깥 채집」이 영영 0 이 되고,
			//   확인하려던 바로 그것(바깥에서 정수가 나는가)이 확인되지 않는다(지난 실행이 그랬다).
			List<Vector3> byDistance = new(match.ActiveResourceNodeLocalPositions);
			Vector3 coreLocal = (Vector3)match.StageRoot.InverseTransformPoint(core);
			byDistance.Sort((left, right) =>
				(right - coreLocal).sqrMagnitude.CompareTo((left - coreLocal).sqrMagnitude));

			int harvestersBefore = match.HarvesterCount;
			int tried = 0;
			foreach (Vector3 local in byDistance)
			{
				if (tried >= 20)
					break;
				tried++;
				match.TryPlaceHarvester((Vector3)match.StageRoot.TransformPoint(local));
			}

			Debug.Log(TAG + " HARVESTERS " + harvestersBefore + " → " + match.HarvesterCount
				+ "  ·  바깥 " + match.OuterHarvesters + " (이어짐 " + match.SuppliedOuterHarvesters + ")"
				+ "  ·  다음 정산 정수 " + match.NextWaveEssence);

			if (match.SuppliedOuterHarvesters > 0 && match.NextWaveEssence > 0)
				Debug.Log(TAG + " ESSENCE-OK 이어진 바깥 채집이 정수를 낸다 ✔");
			else
				Debug.LogWarning(TAG + " ESSENCE-PENDING 아직 확인 못 함 — 위 수치로 원인을 가른다.");
		}
	}
}
