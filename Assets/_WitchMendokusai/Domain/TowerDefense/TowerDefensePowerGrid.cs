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

		// 신호장 — 전기는 「독립된 원」이 아니라 코어에서 사슬을 타고 번지는 것이다(컨트롤넷 레퍼런스).
		// 노드 0 = 코어(스스로 낸다), 나머지 = 발전 인형(받아야만 산다 = 중계탑).
		// 코어는 판에 하나 — 인형처럼 파괴·재생성되지 않으므로 이름표를 박아둔다(0 은 인스턴스 ID 로 안 나온다).
		private const int CORE_KEY = 0;

		// 인형마다 붙는 이름표 — *그 인형이 서 있는 동안* 바뀌지 않아야 충전값이 이어진다.
		// 좌표로 짝지으면 살아 있는 인형이 조금만 흔들려도 매 프레임 「처음 보는 노드」가 된다.
		private readonly Dictionary<Transform, int> generatorKeys = new();
		private int nextGeneratorKey = 1;

		private int KeyOf(Transform generator)
		{
			if (generatorKeys.TryGetValue(generator, out int key))
				return key;

			key = nextGeneratorKey++;
			generatorKeys[generator] = key;
			return key;
		}

		private readonly TowerDefenseSignalField field = new();
		private readonly List<TowerDefenseSignalField.Node> fieldNodes = new();

		/// <summary> 화면이 신호장을 그릴 때 읽는다 — 어디까지 덮였고 얼마나 찼는지. </summary>
		public TowerDefenseSignalField Field => field;

		/// <summary> 전체 용량 / 요구 — 화면이 「얼마나 모자라나」를 말한다. </summary>
		public int Capacity { get; private set; }
		public int Demand { get; private set; }

		/// <summary> 전기를 못 받아 멈춘 건물 수. </summary>
		public int UnpoweredBuildings { get; private set; }

		/// <summary> 판을 새로 시작한다. </summary>
		public void Clear()
		{
			// ★ 신호장도 비운다. 안 비우면 코어 이름표가 고정값이라 **다음 판이 이미 가득 찬 채로 시작한다**
			//   — 「즉시 안 켜지고 점점 채워진다」가 두 번째 판부터 통째로 사라진다(사용자가 콕 집어 요구한 것).
			field.Clear();
			generators.Clear();
			generatorKeys.Clear();
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
			System.Func<Transform, bool> isHarvester, System.Func<Transform, TowerDefenseDollLabel> findLabel,
			float deltaTime)
		{
			if (stage == null || stage.CorePowerCapacity <= 0)
				return;

			// ★ 신호장을 먼저 세운다. 코어만 스스로 신호를 내고, 발전 인형은 *받아서 넘긴다*.
			//   그래서 사슬 중간이 끊기면 그 너머가 통째로 죽는다(컨트롤넷 — 사용자 지시).
			fieldNodes.Clear();
			// 코어의 이름표는 고정값 — 코어는 판에 하나뿐이고 자리가 흔들려도 같은 코어다.
			fieldNodes.Add(new TowerDefenseSignalField.Node(CORE_KEY, corePosition, stage.CorePowerRadius, true));
			for (int index = generators.Count - 1; index >= 0; index--)
			{
				if (generators[index] == null)
					generators.RemoveAt(index);
			}
			foreach (Transform generator in generators)
			{
				fieldNodes.Add(new TowerDefenseSignalField.Node(
					KeyOf(generator), generator.position, stage.GeneratorRadius, false));
			}

			field.Configure(fieldNodes);

			// 시간을 흘려 신호를 번지게 한다 — 여기가 「즉시 안 켜지고 점점 채워진다」의 자리다.
			field.Tick(deltaTime, stage.SignalChargeSeconds, stage.SignalDrainSeconds);

			// ★ 공급원의 반경은 *지금 찬 만큼*이다 — 그래서 원이 자라며 채워지고, 끊기면 물 빠지듯 줄어든다.
			//   아직 신호가 안 닿은 발전 인형은 용량도 0 이다(꽂혀 있어도 안 도는 것).
			sources.Clear();
			sources.Add(new TowerDefensePower.Source(
				corePosition, field.LiveRadiusAt(0), stage.CorePowerCapacity + bonusCapacity));
			for (int index = 1; index < fieldNodes.Count; index++)
			{
				sources.Add(new TowerDefensePower.Source(
					fieldNodes[index].Position,
					field.LiveRadiusAt(index),
					field.IsFed(index) ? stage.GeneratorCapacity : 0));
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
				{
					label.Unpowered = hasPower == false;
					// 신호장이 그 자리를 덮고 있느냐가 두 이유를 가른다 — 덮였는데 안 돌면 용량이 모자란 것.
					label.OutOfSignal = hasPower == false && field.IsCovered(consumer.position) == false;
				}
			}
		}
	}
}
