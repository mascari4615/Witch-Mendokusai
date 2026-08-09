using NUnit.Framework;
using UnityEngine;
// 이 시험이 만지는 건 엔진 쪽 값이다(화면 배치·충돌체·모터 문맥) — 좌표 별칭 없음 (TASK-WM-214).

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-182 Phase 1 — <see cref="BT_Flee.FleeDirection"/> 도주 방향 회귀 잠금.
	///
	/// 순수 함수 — 플레이어 반대방향 단위 벡터 + 추격(BT_MoveToPlayer)의 정반대 불변식만 검증.
	/// MonoBehaviour/UnitObject/Camera/PlayMode 0 — static 호출 직접.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class WildBeastFleeTest
	{
		[Test]
		public void FleeDirection_PointsAwayFromPlayer()
		{
			Vector3 beastPosition = new(5f, 0f, 0f);
			Vector3 playerPosition = new(0f, 0f, 0f);

			Vector3 fleeDirection = BT_Flee.FleeDirection(beastPosition, playerPosition);

			// 야수는 플레이어(원점)로부터 멀어지는 +x 방향으로 도주
			Assert.That(fleeDirection.x, Is.GreaterThan(0f), "도주 방향은 플레이어 반대편(+x)을 향해야 함");
			Assert.That(fleeDirection.magnitude, Is.EqualTo(1f).Within(0.001f), "도주 방향은 단위 벡터");
		}

		[Test]
		public void FleeDirection_IsExactOppositeOfChase()
		{
			Vector3 beastPosition = new(3f, 0f, 4f);
			Vector3 playerPosition = new(-2f, 0f, 1f);

			Vector3 fleeDirection = BT_Flee.FleeDirection(beastPosition, playerPosition);
			// BT_MoveToPlayer 의 추격 방향 = (player - self) 정규화
			Vector3 chaseDirection = (playerPosition - beastPosition).normalized;

			Assert.That(fleeDirection.x, Is.EqualTo(-chaseDirection.x).Within(0.001f));
			Assert.That(fleeDirection.y, Is.EqualTo(-chaseDirection.y).Within(0.001f));
			Assert.That(fleeDirection.z, Is.EqualTo(-chaseDirection.z).Within(0.001f));
		}

		[Test]
		public void FleeDirection_TowardPositiveZ_WhenPlayerBehindOnZ()
		{
			Vector3 beastPosition = new(0f, 0f, 10f);
			Vector3 playerPosition = new(0f, 0f, 2f);

			Vector3 fleeDirection = BT_Flee.FleeDirection(beastPosition, playerPosition);

			// 플레이어가 -z 쪽 → 야수는 +z 로 도주
			Assert.That(fleeDirection.z, Is.GreaterThan(0f), "플레이어가 z 작은 쪽이면 야수는 z 큰 쪽으로 도주");
			Assert.That(fleeDirection.x, Is.EqualTo(0f).Within(0.001f), "x 변위 없음");
		}
	}
}
