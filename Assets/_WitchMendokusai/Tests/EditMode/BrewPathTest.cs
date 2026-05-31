using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-174 Phase 0 — 「솥 속의 지도」 벡터 항해 코어 회귀 잠금.
	///
	/// AlchemyVector + EffectCoord + HazardZone + BrewPath 순수 POCO 결정성·도달·관통 검증.
	/// MonoBehaviour/VContainer/Unity 런타임 0 — new() 직접. RciDemandModelTest 동격.
	///
	/// Phase0 는 "재료 2~3개 → 벡터 합성 → 목표 좌표 도달 + 위험지대 관통 여부" 의 최소 루프만 잠근다.
	/// 강도/품질/부작용 정량화·캐릭터(링/알리사) 성향 모디파이어는 Phase3 — 본 슬라이스 범위 밖.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class BrewPathTest
	{
		[Test]
		public void NewPath_StartsAtOrigin_WithZeroSteps()
		{
			BrewPath path = new BrewPath(new AlchemyVector(0f, 0f));

			Assert.That(path.StepCount, Is.Zero, "갓 만든 경로는 step 0");
			Assert.That(path.Position.X, Is.EqualTo(0f), "Position = Origin");
			Assert.That(path.Position.Y, Is.EqualTo(0f), "Position = Origin");
			Assert.That(path.Waypoints.Count, Is.EqualTo(1), "Origin 자체가 첫 waypoint");
		}

		[Test]
		public void ApplyVector_MovesPosition_ByExactForce()
		{
			BrewPath path = new BrewPath(new AlchemyVector(1f, 2f));

			path.Apply(new AlchemyVector(3f, 4f));

			Assert.That(path.Position.X, Is.EqualTo(4f), "X = 1 + 3");
			Assert.That(path.Position.Y, Is.EqualTo(6f), "Y = 2 + 4");
			Assert.That(path.StepCount, Is.EqualTo(1), "Apply 1 회 = step 1");
		}

		[Test]
		public void ApplyMultiple_AccumulatesAdditively()
		{
			BrewPath path = new BrewPath(AlchemyVector.Zero);

			path.Apply(new AlchemyVector(1f, 0f));
			path.Apply(new AlchemyVector(0f, 1f));
			path.Apply(new AlchemyVector(2f, 3f));

			Assert.That(path.Position.X, Is.EqualTo(3f), "X 누적 = 1+0+2");
			Assert.That(path.Position.Y, Is.EqualTo(4f), "Y 누적 = 0+1+3");
			Assert.That(path.Waypoints.Count, Is.EqualTo(4), "Origin + 3 step");
		}

		[Test]
		public void ApplyOrderIndependence_SameSet_SameEndpoint()
		{
			// 벡터 덧셈 가환 — 재료 순서 바꿔도 최종 효과 좌표 동일. WM 디제틱: "같은 재료를 다르게 넣으면
			// 도착지는 같지만 길은 다르다" — 경로별 hazard 카운트 차이가 부작용 분기의 시드.
			BrewPath pathA = new BrewPath(AlchemyVector.Zero);
			pathA.Apply(new AlchemyVector(2f, 1f));
			pathA.Apply(new AlchemyVector(-1f, 3f));

			BrewPath pathB = new BrewPath(AlchemyVector.Zero);
			pathB.Apply(new AlchemyVector(-1f, 3f));
			pathB.Apply(new AlchemyVector(2f, 1f));

			Assert.That(pathB.Position.X, Is.EqualTo(pathA.Position.X), "순서 무관 끝점 X 동일");
			Assert.That(pathB.Position.Y, Is.EqualTo(pathA.Position.Y), "순서 무관 끝점 Y 동일");
		}

		[Test]
		public void EffectCoord_AcceptsArrival_WhenWithinTolerance()
		{
			EffectCoord target = new EffectCoord(new AlchemyVector(10f, 10f), 1.5f);

			Assert.That(target.ContainsArrival(new AlchemyVector(10f, 10f)), Is.True, "정확히 중심 = 도달");
			Assert.That(target.ContainsArrival(new AlchemyVector(11f, 10f)), Is.True, "tolerance(1.5) 안 = 도달");
		}

		[Test]
		public void EffectCoord_RejectsArrival_WhenBeyondTolerance()
		{
			EffectCoord target = new EffectCoord(new AlchemyVector(10f, 10f), 1.5f);

			Assert.That(target.ContainsArrival(new AlchemyVector(12f, 10f)), Is.False, "tolerance 초과 = 미도달");
			Assert.That(target.ContainsArrival(new AlchemyVector(0f, 0f)), Is.False, "원거리 = 미도달");
		}

		[Test]
		public void BrewPath_HasArrived_WhenPositionWithinTarget()
		{
			EffectCoord target = new EffectCoord(new AlchemyVector(5f, 5f), 0.5f);
			BrewPath path = new BrewPath(AlchemyVector.Zero);

			path.Apply(new AlchemyVector(5f, 5f));

			Assert.That(path.HasArrived(target), Is.True, "벡터 합성 = 목표 → 도달");
		}

		[Test]
		public void BrewPath_HasNotArrived_WhenPositionOffTarget()
		{
			EffectCoord target = new EffectCoord(new AlchemyVector(5f, 5f), 0.5f);
			BrewPath path = new BrewPath(AlchemyVector.Zero);

			path.Apply(new AlchemyVector(3f, 5f));

			Assert.That(path.HasArrived(target), Is.False, "벡터 합성 ≠ 목표(허용 밖) → 미도달");
		}

		[Test]
		public void HazardZone_ContainsPoint_WhenInsideRadius()
		{
			HazardZone zone = new HazardZone(new AlchemyVector(0f, 0f), 2f);

			Assert.That(zone.Contains(new AlchemyVector(0f, 0f)), Is.True, "중심 = 안");
			Assert.That(zone.Contains(new AlchemyVector(1f, 1f)), Is.True, "반경 내 = 안");
			Assert.That(zone.Contains(new AlchemyVector(3f, 0f)), Is.False, "반경 밖 = 밖");
		}

		[Test]
		public void HazardZone_IntersectsSegment_WhenPathPassesThrough()
		{
			HazardZone zone = new HazardZone(new AlchemyVector(5f, 0f), 1f);

			// 선분이 위험지대 한가운데(5,0)를 정통 관통.
			Assert.That(zone.IntersectsSegment(new AlchemyVector(0f, 0f), new AlchemyVector(10f, 0f)),
				Is.True, "직선 관통 = 교차");
		}

		[Test]
		public void HazardZone_IntersectsSegment_WhenEndpointInside()
		{
			HazardZone zone = new HazardZone(new AlchemyVector(5f, 5f), 1f);

			// 시작은 밖, 끝이 안. 들어가서 멈춰도 "닿음" = 관통.
			Assert.That(zone.IntersectsSegment(new AlchemyVector(0f, 0f), new AlchemyVector(5f, 5f)),
				Is.True, "끝점이 안쪽 = 교차");
		}

		[Test]
		public void HazardZone_NoIntersection_WhenSegmentFarAway()
		{
			HazardZone zone = new HazardZone(new AlchemyVector(5f, 0f), 1f);

			// 선분이 위험지대에서 한참 떨어진 평행선.
			Assert.That(zone.IntersectsSegment(new AlchemyVector(0f, 10f), new AlchemyVector(10f, 10f)),
				Is.False, "원거리 평행선 = 교차 없음");
		}

		[Test]
		public void HazardZone_DegenerateSegment_FallsBackToContains()
		{
			// 같은 점으로 시작/끝 = 길이 0 선분 → Contains 로 판단.
			HazardZone zone = new HazardZone(new AlchemyVector(0f, 0f), 1f);

			Assert.That(zone.IntersectsSegment(new AlchemyVector(0f, 0f), new AlchemyVector(0f, 0f)),
				Is.True, "0-길이 선분 안쪽 = 교차");
			Assert.That(zone.IntersectsSegment(new AlchemyVector(5f, 5f), new AlchemyVector(5f, 5f)),
				Is.False, "0-길이 선분 바깥 = 교차 없음");
		}

		[Test]
		public void BrewPath_CountsHazardCrossings_AlongMultiSegmentPath()
		{
			// 같은 효과 좌표(7,0)로 가는 두 길 — 직선은 위험지대 관통, 우회는 안 관통.
			HazardZone zone = new HazardZone(new AlchemyVector(3.5f, 0f), 1f);

			BrewPath direct = new BrewPath(AlchemyVector.Zero);
			direct.Apply(new AlchemyVector(7f, 0f));

			BrewPath detour = new BrewPath(AlchemyVector.Zero);
			detour.Apply(new AlchemyVector(0f, 3f));
			detour.Apply(new AlchemyVector(7f, 0f));
			detour.Apply(new AlchemyVector(0f, -3f));

			Assert.That(direct.CountHazardCrossings(zone), Is.EqualTo(1), "직선 = 위험지대 관통");
			Assert.That(detour.CountHazardCrossings(zone), Is.Zero, "y=3 우회 = 관통 없음");
		}

		[Test]
		public void BrewPath_CountsRepeatedCrossings_InAndOut()
		{
			// 같은 지대를 들락날락하면 각 관통 segment 별로 카운트 — Phase3 부작용 강도 시그널의 시드.
			HazardZone zone = new HazardZone(new AlchemyVector(0f, 0f), 1f);

			BrewPath path = new BrewPath(new AlchemyVector(-3f, 0f));
			path.Apply(new AlchemyVector(6f, 0f));   // -3,0 → 3,0  (관통)
			path.Apply(new AlchemyVector(-6f, 0f));  // 3,0 → -3,0  (재관통)

			Assert.That(path.CountHazardCrossings(zone), Is.EqualTo(2), "두 번 들어갔다 나오면 2 카운트");
		}

		[Test]
		public void BrewPath_DeterminismLock_SameSequence_SameWaypoints()
		{
			// 결정성 회귀 잠금: 같은 origin + 같은 force 시퀀스 → 같은 waypoint 시퀀스.
			// EditMode 회귀 9.5/10 ceiling — 솥 물리가 회귀 없이 박힌다.
			AlchemyVector origin = new AlchemyVector(2f, -1f);
			AlchemyVector[] forces =
			{
				new AlchemyVector(1f, 0.5f),
				new AlchemyVector(-2f, 3f),
				new AlchemyVector(0.25f, -0.75f)
			};

			BrewPath pathA = new BrewPath(origin);
			BrewPath pathB = new BrewPath(origin);
			for (int i = 0; i < forces.Length; i++)
			{
				pathA.Apply(forces[i]);
				pathB.Apply(forces[i]);
			}

			Assert.That(pathB.Waypoints.Count, Is.EqualTo(pathA.Waypoints.Count), "waypoint 수 동일");
			for (int i = 0; i < pathA.Waypoints.Count; i++)
			{
				Assert.That(pathB.Waypoints[i].X, Is.EqualTo(pathA.Waypoints[i].X), "waypoint X 결정성");
				Assert.That(pathB.Waypoints[i].Y, Is.EqualTo(pathA.Waypoints[i].Y), "waypoint Y 결정성");
			}
		}
	}
}
