using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-3 — <see cref="CommuteMatchModel"/> 통근 노동력 매칭 회귀 잠금.
	///
	/// 순수 함수 — 주거 노동력을 "도로 연결된" 직장 일자리에 그리디 배정. 연결성은 isReachable 델리게이트
	/// 주입(RoadGraph 미의존 = 테스트 격리). 취업/미충원/실업 보존 불변식 검증. new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CommuteMatchModelTest
	{
		private static readonly Vector3Int HOME_A = new(0, 0, 0);
		private static readonly Vector3Int HOME_C = new(10, 0, 0);
		private static readonly Vector3Int WORK_B = new(5, 0, 0);

		private static bool AlwaysReachable(Vector3Int from, Vector3Int to) => true;

		private static bool NeverReachable(Vector3Int from, Vector3Int to) => false;

		[Test]
		public void FullMatch_AllEmployed()
		{
			CommuteMatchModel model = new();
			List<LaborSource> sources = new() { new(HOME_A, 3) };
			List<JobSite> sites = new() { new(WORK_B, 3) };

			CommuteMatchResult result = model.Evaluate(sources, sites, AlwaysReachable);

			Assert.That(result.Employed, Is.EqualTo(3), "노동=일자리=3 → 전원 취업");
			Assert.That(result.UnfilledJobs, Is.EqualTo(0));
			Assert.That(result.Unemployed, Is.EqualTo(0));
		}

		[Test]
		public void ExcessJobs_SomeUnfilled()
		{
			CommuteMatchModel model = new();
			List<LaborSource> sources = new() { new(HOME_A, 2) };
			List<JobSite> sites = new() { new(WORK_B, 5) };

			CommuteMatchResult result = model.Evaluate(sources, sites, AlwaysReachable);

			Assert.That(result.Employed, Is.EqualTo(2));
			Assert.That(result.UnfilledJobs, Is.EqualTo(3), "일자리 5 - 노동 2 = 3 미충원");
			Assert.That(result.Unemployed, Is.EqualTo(0));
		}

		[Test]
		public void ExcessLabor_SomeUnemployed()
		{
			CommuteMatchModel model = new();
			List<LaborSource> sources = new() { new(HOME_A, 5) };
			List<JobSite> sites = new() { new(WORK_B, 2) };

			CommuteMatchResult result = model.Evaluate(sources, sites, AlwaysReachable);

			Assert.That(result.Employed, Is.EqualTo(2));
			Assert.That(result.UnfilledJobs, Is.EqualTo(0));
			Assert.That(result.Unemployed, Is.EqualTo(3), "노동 5 - 일자리 2 = 3 실업");
		}

		[Test]
		public void Disconnected_NoMatch()
		{
			CommuteMatchModel model = new();
			List<LaborSource> sources = new() { new(HOME_A, 4) };
			List<JobSite> sites = new() { new(WORK_B, 4) };

			CommuteMatchResult result = model.Evaluate(sources, sites, NeverReachable);

			Assert.That(result.Employed, Is.EqualTo(0), "도로 미연결 = 매칭 0");
			Assert.That(result.UnfilledJobs, Is.EqualTo(4));
			Assert.That(result.Unemployed, Is.EqualTo(4));
		}

		[Test]
		public void PartialReachability_OnlyConnectedMatched()
		{
			CommuteMatchModel model = new();
			// A 는 직장 닿음, C 는 못 닿음.
			List<LaborSource> sources = new() { new(HOME_A, 2), new(HOME_C, 2) };
			List<JobSite> sites = new() { new(WORK_B, 2) };

			CommuteMatchResult result = model.Evaluate(sources, sites, (from, to) => from == HOME_A);

			Assert.That(result.Employed, Is.EqualTo(2), "연결된 A 만 취업");
			Assert.That(result.UnfilledJobs, Is.EqualTo(0), "일자리 2 = A 가 채움");
			Assert.That(result.Unemployed, Is.EqualTo(2), "미연결 C 의 2명 실업");
		}
	}
}
