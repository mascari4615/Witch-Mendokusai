using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전기(TASK-WM-194) — 건물은 *전기를 받아야* 돈다.
	///
	/// ★ 왜 필요한가 (사용자 지시: "도시 건설 게임들처럼 전기 개념이 있어서 건물들이 전기가 충족되어야
	///   동작하게. 전기 건물을 설치하면 일정 범위 안에 일정 량의 전기를 공급"): 지금은 자원만 있으면
	///   어디든 몇 개든 지을 수 있다. 전기가 있으면 「지을 수 있나」가 돈 문제에서 **자리와 살림 문제**로
	///   바뀐다 — 발전을 먼저 깔아야 방어를 늘릴 수 있고, 발전 자체도 지켜야 할 것이 된다.
	/// ★ 보급과 다른 층이다: 보급은 *코어까지 이어졌나*(사슬), 전기는 *덮였나 + 용량이 남았나*(범위+총량).
	///   둘을 하나로 합치면 「넓힌다」의 대가가 한 종류로 뭉개진다.
	///
	/// 배분 규칙: 소비자는 자기를 덮는 공급원 중 *가장 가까운* 것부터 받는다. 용량이 다 차면 다음 공급원,
	/// 그것도 없으면 **정지**. 같은 입력이면 언제나 같은 결과(순서 의존 없음 — 거리로 정렬).
	///
	/// 순수 정적 — 씬·RNG 0. EditMode 로 전량 검증.
	/// </summary>
	public static class TowerDefensePower
	{
		/// <summary> 전기를 내는 것(코어·발전 인형). </summary>
		public readonly struct Source
		{
			public readonly Vector3 Position;
			public readonly float Radius;
			public readonly int Capacity;

			public Source(Vector3 position, float radius, int capacity)
			{
				Position = position;
				Radius = radius;
				Capacity = capacity;
			}
		}

		/// <summary> 전기를 먹는 것(포탑·채집 등). </summary>
		public readonly struct Consumer
		{
			public readonly Vector3 Position;
			public readonly int Demand;

			public Consumer(Vector3 position, int demand)
			{
				Position = position;
				Demand = demand;
			}
		}

		/// <summary>
		/// 누가 전기를 받는지 계산한다. powered 에 *받는 소비자의 인덱스*가 담긴다.
		/// 요구량 0 이하인 것은 전기가 필요 없는 것으로 보고 항상 받는다(벽·함정 같은 것).
		/// </summary>
		public static void Compute(
			IReadOnlyList<Source> sources,
			IReadOnlyList<Consumer> consumers,
			HashSet<int> powered,
			List<int> remainingCapacity = null)
		{
			powered.Clear();
			if (consumers == null || consumers.Count == 0)
				return;

			int sourceCount = sources != null ? sources.Count : 0;
			List<int> remaining = remainingCapacity ?? new List<int>(sourceCount);
			remaining.Clear();
			for (int index = 0; index < sourceCount; index++)
				remaining.Add(sources[index].Capacity);

			for (int consumerIndex = 0; consumerIndex < consumers.Count; consumerIndex++)
			{
				Consumer consumer = consumers[consumerIndex];
				if (consumer.Demand <= 0)
				{
					powered.Add(consumerIndex); // 전기가 필요 없는 것.
					continue;
				}

				// 덮는 공급원 중 가장 가까운 것부터 — 거리로 정하니 목록 순서가 결과를 바꾸지 않는다.
				int bestSource = -1;
				float bestDistance = float.MaxValue;
				for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
				{
					if (remaining[sourceIndex] < consumer.Demand)
						continue;

					Source source = sources[sourceIndex];
					float distance = Vector3.Distance(source.Position, consumer.Position);
					if (distance > source.Radius || distance >= bestDistance)
						continue;

					bestSource = sourceIndex;
					bestDistance = distance;
				}

				if (bestSource < 0)
					continue; // 덮이지 않았거나 남은 용량이 없다 — 이 건물은 선다.

				remaining[bestSource] -= consumer.Demand;
				powered.Add(consumerIndex);
			}
		}

		/// <summary> 전체 용량 / 전체 요구 — 화면이 「얼마나 모자라나」를 말할 때 쓴다. </summary>
		public static int TotalCapacity(IReadOnlyList<Source> sources)
		{
			int total = 0;
			for (int index = 0; sources != null && index < sources.Count; index++)
				total += sources[index].Capacity;
			return total;
		}

		public static int TotalDemand(IReadOnlyList<Consumer> consumers)
		{
			int total = 0;
			for (int index = 0; consumers != null && index < consumers.Count; index++)
				total += Mathf.Max(0, consumers[index].Demand);
			return total;
		}
	}
}
