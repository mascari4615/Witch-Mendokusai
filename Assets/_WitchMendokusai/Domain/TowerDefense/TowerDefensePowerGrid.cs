using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 판의 전기 — 누가 대주고 누가 먹는지, 그래서 무엇이 멈추는지 (TASK-WM-194).
	///
	/// ★ 왜 떼어냈나: 이 층이 매치 본체 안에 섞여 있었다. 매치는 이미 4000줄이 넘고, 오늘 잡은 결함
	///   여럿이 *한 덩어리가 너무 많은 걸 알아서* 생긴 종류였다(판 것이 계속 전기를 먹던 누수가 그중 하나).
	///   전기는 「범위에 덮였나 + 총량이 남았나」만 알면 되는 자립적인 규칙이라 가장 먼저 떨어져 나온다.
	/// ★ 보급과 다른 층이다: 보급은 *코어까지 이어졌나*(사슬), 전기는 *덮였나 + 남았나*(범위+총량).
	///   둘을 합치면 「넓힌다」의 대가가 한 종류로 뭉개진다.
	///
	/// 계산 자체는 규칙층(<see cref="TowerDefensePower"/>)이 한다 — 여기는 *씬의 것들*(Transform)을
	/// 그 계산에 넣고, 결과를 다시 씬에 되돌리는 일만 한다.
	/// </summary>
	public sealed class TowerDefensePowerGrid
	{
		private readonly List<TowerDefensePower.Source> sources = new();
		private readonly List<TowerDefensePower.Consumer> consumers = new();
		private readonly List<Transform> consumerTransforms = new();
		private readonly HashSet<int> powered = new();
		private readonly List<Transform> generators = new();

		/// <summary> 전체 용량 / 요구 — 화면이 「얼마나 모자라나」를 말한다. </summary>
		public int Capacity { get; private set; }
		public int Demand { get; private set; }

		/// <summary> 전기를 못 받아 멈춘 건물 수. </summary>
		public int UnpoweredBuildings { get; private set; }

		/// <summary> 판을 새로 시작한다. </summary>
		public void Clear()
		{
			generators.Clear();
			consumerTransforms.Clear();
			powered.Clear();
			Capacity = 0;
			Demand = 0;
			UnpoweredBuildings = 0;
		}

		public void AddGenerator(Transform generator) => generators.Add(generator);
		public bool RemoveGenerator(Transform generator) => generators.Remove(generator);

		public void AddConsumer(Transform consumer) => consumerTransforms.Add(consumer);
		public void RemoveConsumer(Transform consumer) => consumerTransforms.Remove(consumer);

		/// <summary> 그 건물이 지금 전기를 받고 있나 — 목록에 없는 것(발전·연구)은 안 먹으므로 늘 true. </summary>
		public bool IsPowered(Transform building)
		{
			int index = consumerTransforms.IndexOf(building);
			return index < 0 || powered.Contains(index);
		}

		/// <summary>
		/// 지금 상태로 다시 계산하고, 그 결과를 건물들에 되돌린다(멈추거나 다시 돌거나).
		/// isHarvester = 그 건물이 채집인가(먹는 양이 다르다) — 매치만 아는 것이라 물어서 쓴다.
		/// </summary>
		public void Refresh(TowerDefenseStageSO stage, Vector3 corePosition, int bonusCapacity,
			System.Func<Transform, bool> isHarvester, System.Func<Transform, TowerDefenseDollLabel> findLabel)
		{
			if (stage == null || stage.CorePowerCapacity <= 0)
				return;

			sources.Clear();
			sources.Add(new TowerDefensePower.Source(
				corePosition, stage.CorePowerRadius, stage.CorePowerCapacity + bonusCapacity));
			for (int index = generators.Count - 1; index >= 0; index--)
			{
				if (generators[index] == null)
				{
					generators.RemoveAt(index);
					continue;
				}
				sources.Add(new TowerDefensePower.Source(
					generators[index].position, stage.GeneratorRadius, stage.GeneratorCapacity));
			}

			consumers.Clear();
			for (int index = consumerTransforms.Count - 1; index >= 0; index--)
			{
				if (consumerTransforms[index] == null)
					consumerTransforms.RemoveAt(index);
			}
			foreach (Transform consumer in consumerTransforms)
			{
				int demand = isHarvester != null && isHarvester(consumer)
					? stage.HarvesterPowerDemand
					: stage.TowerPowerDemand;
				consumers.Add(new TowerDefensePower.Consumer(consumer.position, demand));
			}

			TowerDefensePower.Compute(sources, consumers, powered);

			Capacity = TowerDefensePower.TotalCapacity(sources);
			Demand = TowerDefensePower.TotalDemand(consumers);
			UnpoweredBuildings = consumers.Count - powered.Count;

			// 전기를 못 받으면 *멈춘다* — 포탑은 쏘지 않고, 이름표가 그 사실을 말한다.
			for (int index = 0; index < consumerTransforms.Count; index++)
			{
				Transform consumer = consumerTransforms[index];
				bool hasPower = powered.Contains(index);

				TowerDefenseWeapon weapon = consumer.GetComponent<TowerDefenseWeapon>();
				if (weapon != null && weapon.enabled != hasPower)
					weapon.enabled = hasPower;

				// 함정도 같은 규칙 — 전기가 끊기면 밟혀도 안 터진다.
				TowerDefenseTrap trap = consumer.GetComponent<TowerDefenseTrap>();
				if (trap != null && trap.enabled != hasPower)
					trap.enabled = hasPower;

				TowerDefenseDollLabel label = findLabel != null ? findLabel(consumer) : null;
				if (label != null)
					label.Unpowered = hasPower == false;
			}
		}
	}
}
