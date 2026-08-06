using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 신호장을 눈에 보이게 그린다 — 「어디까지가 내 땅인가」 (TASK-WM-194, 컨트롤넷 레퍼런스).
	///
	/// ★ 왜 필요한가: 전기가 규칙으로만 있으면 「왜 이 포탑이 안 돌지」가 화면 어디에도 안 나온다.
	///   덮인 자리에 테두리가 서고, 신호가 찰수록 그 원이 자라며 밝아지면 넓히는 일이 눈에 보인다.
	/// ★ 파동(사용자 지시): 신호를 내는 곳에서 링이 주기적으로 퍼져 나간다. 사슬을 타고 이어지는 것이
	///   *움직임*으로 보여야 「신호가 흐른다」가 되고, 안 그러면 그냥 원 몇 개다.
	///
	/// 아트 에셋 0 — <see cref="TowerDefenseRing"/> 를 빌려 쓴다(런타임 셰이더 조회).
	/// </summary>
	public sealed class TowerDefenseSignalView : MonoBehaviour
	{
		// 노드 하나가 갖는 그림 = 경계 원 하나 + 퍼져 나가는 파동 링 몇 개.
		private sealed class NodeVisual
		{
			public TowerDefenseRing Boundary;
			public readonly List<TowerDefenseRing> Pulses = new();
			public readonly List<float> PulseAges = new();
			public float NextPulseIn;
		}

		private const int MAX_PULSES_PER_NODE = 3; // 이보다 많으면 링이 겹쳐 판이 지저분해진다.

		private readonly List<NodeVisual> visuals = new();
		private Transform stageRoot;

		public static TowerDefenseSignalView Create(Transform parent)
		{
			GameObject viewObject = new GameObject("SignalField");
			viewObject.transform.SetParent(parent, false);

			TowerDefenseSignalView view = viewObject.AddComponent<TowerDefenseSignalView>();
			view.stageRoot = parent;
			return view;
		}

		/// <summary> 신호장 상태를 그대로 그림으로 옮긴다. 매 프레임 부른다. </summary>
		public void Tick(TowerDefenseSignalField field, TowerDefenseStageSO stage, float deltaTime)
		{
			if (field == null || stage == null)
				return;

			while (visuals.Count < field.NodeCount)
				visuals.Add(new NodeVisual());

			for (int index = 0; index < visuals.Count; index++)
			{
				NodeVisual visual = visuals[index];
				bool live = index < field.NodeCount && field.LiveRadiusAt(index) > 0f;

				if (live == false)
				{
					HideAll(visual);
					continue;
				}

				float radius = field.LiveRadiusAt(index);
				float charge = field.ChargeAt(index);
				Vector3 position = field.PositionAt(index);

				DrawBoundary(visual, stage, position, radius, charge);
				TickPulses(visual, stage, position, radius, charge, deltaTime);
			}
		}

		private void DrawBoundary(NodeVisual visual, TowerDefenseStageSO stage, Vector3 position, float radius, float charge)
		{
			if (visual.Boundary == null)
			{
				visual.Boundary = TowerDefenseRing.Create(transform, "SignalEdge", stage.SignalTint, 0.16f, 0.04f);
			}

			// 월드 자리를 그대로 쓴다 — 노드(코어·발전 인형)는 무대 아래 어디든 설 수 있다.
			visual.Boundary.transform.position = position + new Vector3(0f, 0.04f, 0f);
			visual.Boundary.SetRadius(radius);

			Color tint = stage.SignalTint;
			// 찰수록 또렷해진다 — 반쯤 찬 자리는 흐릿해서 「아직 오는 중」으로 읽힌다.
			visual.Boundary.SetColor(new Color(tint.r, tint.g, tint.b, 0.18f + 0.42f * charge));
			visual.Boundary.SetVisible(true);
		}

		private void TickPulses(NodeVisual visual, TowerDefenseStageSO stage, Vector3 position,
			float radius, float charge, float deltaTime)
		{
			if (stage.SignalPulseInterval <= 0f)
			{
				foreach (TowerDefenseRing pulse in visual.Pulses)
					pulse.SetVisible(false);
				return;
			}

			// ★ 링과 나이는 **자리로 짝지어진 고정 풀**이다(나이 < 0 = 쉬는 중).
			//   나이 목록만 줄이면 두 목록의 번호가 어긋나 엉뚱한 링이 움직인다 — 파동이 튀는 그림이 그것.
			while (visual.Pulses.Count < MAX_PULSES_PER_NODE)
			{
				visual.Pulses.Add(TowerDefenseRing.Create(transform, "SignalPulse", stage.SignalTint, 0.3f, 0.05f));
				visual.PulseAges.Add(-1f);
			}

			visual.NextPulseIn -= deltaTime;
			if (visual.NextPulseIn <= 0f)
			{
				visual.NextPulseIn = stage.SignalPulseInterval;
				for (int index = 0; index < visual.PulseAges.Count; index++)
				{
					if (visual.PulseAges[index] >= 0f)
						continue;
					visual.PulseAges[index] = 0f; // 쉬고 있던 링 하나를 내보낸다.
					break;
				}
			}

			for (int index = 0; index < visual.PulseAges.Count; index++)
			{
				if (visual.PulseAges[index] < 0f)
				{
					visual.Pulses[index].SetVisible(false);
					continue;
				}

				visual.PulseAges[index] += deltaTime;
				float ratio = visual.PulseAges[index] / Mathf.Max(0.05f, stage.SignalPulseTravelSeconds);
				if (ratio >= 1f)
				{
					visual.Pulses[index].SetVisible(false);
					visual.PulseAges[index] = -1f;
					continue;
				}

				TowerDefenseRing pulse = visual.Pulses[index];
				pulse.transform.position = position + new Vector3(0f, 0.05f, 0f);
				pulse.SetRadius(Mathf.Max(0.05f, radius * ratio));

				Color tint = stage.SignalTint;
				// 멀어질수록 옅어진다 — 끝에서 툭 사라지면 파동이 아니라 깜빡임으로 보인다.
				pulse.SetColor(new Color(tint.r, tint.g, tint.b, (1f - ratio) * 0.55f * charge));
				pulse.SetVisible(true);
			}
		}

		private static void HideAll(NodeVisual visual)
		{
			if (visual.Boundary != null)
				visual.Boundary.SetVisible(false);
			foreach (TowerDefenseRing pulse in visual.Pulses)
				pulse.SetVisible(false);
			for (int index = 0; index < visual.PulseAges.Count; index++)
				visual.PulseAges[index] = -1f;
		}
	}
}
