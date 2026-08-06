using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 신호장 — 전기가 「자원 수치」가 아니라 **판을 덮으며 번지는 것**이 된다 (TASK-WM-194).
	///
	/// ★ 레퍼런스: 심시티 Cities of Tomorrow 의 컨트롤넷(사용자 지시). 신호는 코어 한 곳에서만 나고,
	///   중계탑은 신호를 *만드는* 게 아니라 받아서 넘긴다. 그래서 덮인 영역 = 내 문명이 닿은 땅이고,
	///   밖으로 넓히는 것은 곧 **사슬을 끌고 나가는 것**이며, 그 사슬 자체가 지켜야 할 것이 된다.
	///   (중간 탑 하나가 부서지면 그 너머가 통째로 죽는다 — 독립된 원이면 절대 안 생기는 긴장.)
	///
	/// ★ 즉시 켜지지 않는다 (사용자 지시: "점점 채워지는 느낌"). 노드는 *충전값*을 갖는다.
	///   · 앞 사슬에서 신호가 닿아 있으면 찬다. 아니면 빠진다.
	///   · **덮는 반경 = 최대 반경 × 충전값** — 그래서 원이 자라며 채워지고, 끊기면 물 빠지듯 줄어든다.
	///   · 충전이 <see cref="LINK_THRESHOLD"/> 를 넘어야 다음 노드에 신호를 넘긴다 — 반쯤 찬 탑이
	///     다음 탑을 켜면 사슬 전체가 미지근하게 동시에 켜져 「번져 나간다」가 안 보인다.
	///
	/// 순수 규칙 — 씬·RNG 0 (Vector3 는 값 타입 좌표로만 쓴다). EditMode 로 전량 검증.
	/// </summary>
	public sealed class TowerDefenseSignalField
	{
		/// <summary> 이만큼 차야 다음 노드로 신호를 넘긴다. </summary>
		public const float LINK_THRESHOLD = 0.6f;

		/// <summary> 이만큼 차야 그 자리를 「덮였다」로 친다 — 갓 켜진 실낱이 건물을 돌리면 안 된다. </summary>
		public const float LIVE_THRESHOLD = 0.35f;

		/// <summary> 신호를 내거나(코어) 넘기는(중계탑) 것. </summary>
		public readonly struct Node
		{
			public readonly Vector3 Position;
			public readonly float Radius;
			public readonly bool IsOrigin; // true = 스스로 신호를 낸다(코어·발전). false = 받아야만 산다.

			public Node(Vector3 position, float radius, bool isOrigin)
			{
				Position = position;
				Radius = radius;
				IsOrigin = isOrigin;
			}
		}

		private readonly List<Node> nodes = new();
		private readonly List<float> charges = new();
		private readonly List<bool> fed = new();

		public int NodeCount => nodes.Count;

		/// <summary> 노드 목록을 갈아끼운다. 자리·반경이 같은 노드는 충전값을 이어받는다 — 안 그러면 목록이 다시 만들어질 때마다 판 전체가 깜빡인다. </summary>
		public void Configure(IReadOnlyList<Node> next)
		{
			List<float> carried = new(charges);
			List<Node> previous = new(nodes);

			nodes.Clear();
			charges.Clear();
			fed.Clear();

			for (int index = 0; next != null && index < next.Count; index++)
			{
				nodes.Add(next[index]);
				charges.Add(CarryOver(previous, carried, next[index]));
				fed.Add(false);
			}
		}

		private static float CarryOver(List<Node> previous, List<float> carried, Node node)
		{
			for (int index = 0; index < previous.Count && index < carried.Count; index++)
			{
				if (previous[index].Position == node.Position && Mathf.Approximately(previous[index].Radius, node.Radius))
					return carried[index];
			}
			return 0f;
		}

		/// <summary>
		/// 시간을 흘린다. chargeSeconds = 빈 노드가 가득 차는 데 걸리는 시간, drainSeconds = 끊긴 뒤 다 빠지는 시간.
		/// 신호는 **코어에서부터 한 겹씩** 퍼진다 — 사슬 순서대로 차야 「번져 나간다」가 보인다.
		/// </summary>
		public void Tick(float deltaTime, float chargeSeconds, float drainSeconds)
		{
			if (nodes.Count == 0 || deltaTime <= 0f)
				return;

			RecomputeFed();

			float chargeStep = chargeSeconds > 0f ? deltaTime / chargeSeconds : 1f;
			float drainStep = drainSeconds > 0f ? deltaTime / drainSeconds : 1f;

			for (int index = 0; index < nodes.Count; index++)
			{
				charges[index] = fed[index]
					? Mathf.Min(1f, charges[index] + chargeStep)
					: Mathf.Max(0f, charges[index] - drainStep);
			}
		}

		/// <summary>
		/// 누가 신호를 받고 있는지 다시 센다 — 코어에서 시작해 *이미 충분히 찬* 노드가 닿는 곳으로 한 겹씩 넓힌다.
		/// 도달 못 한 노드는 앞이 끊긴 것이므로 빠진다(그 너머가 통째로 죽는 그림이 여기서 나온다).
		/// </summary>
		private void RecomputeFed()
		{
			for (int index = 0; index < nodes.Count; index++)
				fed[index] = nodes[index].IsOrigin;

			bool grew = true;
			while (grew)
			{
				grew = false;
				for (int giver = 0; giver < nodes.Count; giver++)
				{
					// 넘기려면 *자기가* 신호를 받고 있고 충분히 차 있어야 한다.
					if (fed[giver] == false || charges[giver] < LINK_THRESHOLD)
						continue;

					float reach = nodes[giver].Radius * charges[giver];
					for (int taker = 0; taker < nodes.Count; taker++)
					{
						if (fed[taker] || taker == giver)
							continue;
						if (Vector3.Distance(nodes[giver].Position, nodes[taker].Position) > reach)
							continue;

						fed[taker] = true;
						grew = true;
					}
				}
			}
		}

		/// <summary> 그 노드가 지금 신호를 받고 있는가 — 화면이 사슬을 그릴 때 쓴다. </summary>
		public bool IsFed(int nodeIndex) => nodeIndex >= 0 && nodeIndex < fed.Count && fed[nodeIndex];

		/// <summary> 그 노드의 충전값 0~1. 화면의 밝기·원 크기가 이 값을 그대로 쓴다. </summary>
		public float ChargeAt(int nodeIndex) => nodeIndex >= 0 && nodeIndex < charges.Count ? charges[nodeIndex] : 0f;

		/// <summary> 지금 그 노드가 실제로 덮는 반경(= 최대 반경 × 충전값). 자라며 채워지고, 끊기면 줄어든다. </summary>
		public float LiveRadiusAt(int nodeIndex)
		{
			if (nodeIndex < 0 || nodeIndex >= nodes.Count)
				return 0f;
			return charges[nodeIndex] >= LIVE_THRESHOLD ? nodes[nodeIndex].Radius * charges[nodeIndex] : 0f;
		}

		public Vector3 PositionAt(int nodeIndex) => nodeIndex >= 0 && nodeIndex < nodes.Count ? nodes[nodeIndex].Position : Vector3.zero;

		/// <summary> 그 자리가 신호에 덮였는가 — 건물이 도는지 정하는 판정. </summary>
		public bool IsCovered(Vector3 point)
		{
			for (int index = 0; index < nodes.Count; index++)
			{
				float radius = LiveRadiusAt(index);
				if (radius > 0f && Vector3.Distance(nodes[index].Position, point) <= radius)
					return true;
			}
			return false;
		}

		/// <summary> 다 찬 노드 수 / 전체 — 화면이 「신호가 아직 뻗는 중」을 말할 때. </summary>
		public int SettledCount()
		{
			int count = 0;
			for (int index = 0; index < charges.Count; index++)
			{
				if (charges[index] >= 1f)
					count++;
			}
			return count;
		}

		public void Clear()
		{
			nodes.Clear();
			charges.Clear();
			fed.Clear();
		}
	}
}
