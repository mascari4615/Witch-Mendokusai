using NUnit.Framework;
using UnityEngine;
// 모터 시험이 만지는 값은 엔진 쪽이다 — 좌표 별칭 없음 (TASK-WM-214).

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 후속 — 지형에 박혔을 때 빠져나오는가 (<see cref="Motor"/> 의 depenetration).
	///
	/// 매 tick 두 번(이동 전·후) 도는 안전망인데 시험이 0 이었다. 여기가 죽으면 캐릭터가 벽·바위에
	/// 박혀 못 나온다 — 플레이어가 가장 먼저, 가장 크게 겪는 종류의 결함이다. 그리고 박히는 경로는
	/// 하나가 아니다: 스폰 위치가 나쁘거나, 지형이 나중에 생기거나(절차적 생성·건물 배치),
	/// 다른 시스템이 위치를 텔레포트시키거나. 어느 경로든 결국 여기가 빼내야 한다.
	/// </summary>
	public sealed class MotorDepenetrationTest
	{
		private const float GROUND_TOP_Y = 0f;
		private const float WALL_HALF_EXTENT = 1f;

		private static void AddGroundPlate(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, 0f), new Vector3(40f, 1f, 40f));
		}

		/// <summary>원점에 선 기둥. 캐릭터를 그 한복판에 두고 시작한다.</summary>
		private static void AddPillar(MotorTestHarness harness)
		{
			harness.AddGround(new Vector3(0f, 1.5f, 0f), new Vector3(WALL_HALF_EXTENT * 2f, 3f, WALL_HALF_EXTENT * 2f));
		}

		/// <summary>기둥 한복판에서 시작 = 완전히 박힌 상태. 빠져나와서, 다시 안 박혀야 한다.</summary>
		[Test]
		public void StartingInsideGeometry_IsPushedOut_AndStaysOut()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, GROUND_TOP_Y, 0f)))
			{
				AddGroundPlate(harness);
				AddPillar(harness);
				harness.AddContributor(new GravityContributor());

				Assert.That(harness.IsOverlappingGeometry(), Is.True,
					"시작부터 안 박혀 있다 — 이 시험이 depenetration 을 안 건드리고 있다");

				harness.StepMany(30);

				Assert.That(harness.IsOverlappingGeometry(), Is.False,
					$"기둥에서 못 빠져나왔다 (pos={harness.Position}) — 박히면 못 나온다");

				// 빠져나온 뒤 다시 빨려들어가지 않는지 — 밀어내기와 sweep 이 서로를 되돌리면 진동한다.
				Vector3 escaped = harness.Position;
				harness.StepMany(30);

				Assert.That(harness.IsOverlappingGeometry(), Is.False, "빠져나왔다가 다시 박혔다");
				Assert.That((harness.Position - escaped).magnitude, Is.LessThan(0.05f),
					$"빠져나온 뒤에도 계속 밀려난다 ({escaped} → {harness.Position}) — 밀어내기가 안 멈춘다");
			}
		}

		/// <summary>
		/// 살짝만 박힌 경우. 완전히 박힌 경우보다 오히려 이쪽이 실전에 잦다 —
		/// 절차적 지형이 발밑에 생기거나, 좁은 곳에서 다른 유닛에 밀렸을 때.
		/// </summary>
		[Test]
		public void ShallowlyEmbedded_IsPushedOut_WithoutLosingGround()
		{
			// 기둥 옆면(x=1) 을 0.2 만큼 파고든 자세.
			Vector3 start = new(WALL_HALF_EXTENT + MotorTestHarness.CAPSULE_RADIUS - 0.2f, GROUND_TOP_Y, 0f);

			using (MotorTestHarness harness = new(start))
			{
				AddGroundPlate(harness);
				AddPillar(harness);
				harness.AddContributor(new GravityContributor());

				Assert.That(harness.IsOverlappingGeometry(), Is.True, "얕게 박힌 상태로 시작 못 했다 — 시험 전제 확인");

				harness.StepMany(30);

				Assert.That(harness.IsOverlappingGeometry(), Is.False,
					$"얕게 박힌 것도 못 빼냈다 (pos={harness.Position})");
				Assert.That(harness.IsGrounded, Is.True,
					$"빼내면서 땅에서 띄워버렸다 (y={harness.Position.y}) — 밀어내기가 위로만 밀면 이렇게 된다");
				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(0.05f), "빼낸 뒤 높이가 어긋났다");
			}
		}
	}
}
