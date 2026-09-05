using System;
using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 통근 노동력 공급원 — 주거 셀과 그 셀의 노동자(인구) 수.
	public readonly struct LaborSource
	{
		public readonly Vector3Int Cell;
		public readonly int Workers;

		public LaborSource(Vector3Int cell, int workers)
		{
			Cell = cell;
			Workers = workers;
		}
	}

	// 통근 일자리 — 직장 셀과 그 셀의 일자리 수.
	public readonly struct JobSite
	{
		public readonly Vector3Int Cell;
		public readonly int Jobs;

		public JobSite(Vector3Int cell, int jobs)
		{
			Cell = cell;
			Jobs = jobs;
		}
	}

	// 통근 매칭 결과 — 취업(매칭)/미충원 일자리/실업 노동자.
	public readonly struct CommuteMatchResult
	{
		public readonly int Employed;
		public readonly int UnfilledJobs;
		public readonly int Unemployed;

		public CommuteMatchResult(int employed, int unfilledJobs, int unemployed)
		{
			Employed = employed;
			UnfilledJobs = unfilledJobs;
			Unemployed = unemployed;
		}
	}

	// GlassBox 통근 매칭 — 순수 함수(상태 0). 주거 노동력을 "도로로 연결된" 직장 일자리에 그리디 배정.
	//
	// **연결성은 RoadGraph 직접참조 X — isReachable 델리게이트 주입** (순수성·테스트 격리). 게임 배선에선
	// (home,work) => RoadGraph.FindPath(homeAdjRoad, workAdjRoad).Count > 0 류로 INC-1 FindPath 와 결합.
	// 공간 신호(어느 집이 어느 직장 닿나)를 전역 RciDemand 와 별개 레이어로 — RCI 셀별 개조(Phase1 회귀) 회피.
	//
	// 비전-중립 — 노동자=사역마인지 스킨 deferred, 모델은 셀좌표 + 스칼라 수만.
	public sealed class CommuteMatchModel
	{
		public CommuteMatchResult Evaluate(IReadOnlyList<LaborSource> sources, IReadOnlyList<JobSite> sites, Func<Vector3Int, Vector3Int, bool> isReachable)
		{
			int[] siteRemaining = new int[sites.Count];
			for (int i = 0; i < sites.Count; i++)
			{
				siteRemaining[i] = sites[i].Jobs;
			}

			int employed = 0;
			int unemployed = 0;

			foreach (LaborSource source in sources)
			{
				int remainingWorkers = source.Workers;

				for (int i = 0; i < sites.Count; i++)
				{
					if (remainingWorkers <= 0)
					{
						break;
					}

					if (siteRemaining[i] <= 0)
					{
						continue;
					}

					if (isReachable(source.Cell, sites[i].Cell) == false)
					{
						continue;
					}

					int assigned = Mathf.Min(remainingWorkers, siteRemaining[i]);
					employed += assigned;
					remainingWorkers -= assigned;
					siteRemaining[i] -= assigned;
				}

				unemployed += remainingWorkers;
			}

			int unfilledJobs = 0;
			for (int i = 0; i < siteRemaining.Length; i++)
			{
				unfilledJobs += siteRemaining[i];
			}

			return new CommuteMatchResult(employed, unfilledJobs, unemployed);
		}
	}
}
