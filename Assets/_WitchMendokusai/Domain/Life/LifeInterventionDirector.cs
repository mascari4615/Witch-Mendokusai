using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-9 — 4호(플레이어) 개입의 공통 코어 + "도와줘!" 표시. 기둥 2(욕구 문제→개입) 라이브.
	/// 주민이 결핍(문제)이면 머리 위 ⚠ 라벨을 띄워 "누가 도움 필요한지" 보이고, <see cref="HelpResident"/> 로
	/// 그 욕구를 채운다(InterventionModel.ApplyRelief). 입력 두 갈래(부감 클릭 / 4호 근접+키)가 *둘 다* 이 진입점을 부른다:
	/// - 부감 클릭: <see cref="TryHelpAtScreenPoint"/>(카메라·스크린좌표 → 레이캐스트 → HelpResident)
	/// - 4호 근접+키: <see cref="HelpNearest"/>(기준 위치 반경 내 가장 급한 주민)
	/// 입력 *트리거* 배선(InputManager)은 시점/4호 캐릭터와 함께 — 이 director 는 입력 무관 코어(시점·캐릭터 양쪽 재사용).
	/// </summary>
	public class LifeInterventionDirector : MonoBehaviour
	{
		// 한 번 도와줄 때 채우는 양(욕구 상한서 클램프). 수치노출.
		[SerializeField] private float helpAmount = 80f;
		// 4호 근접 개입(B)에서 "옆"으로 보는 반경. 수치노출.
		[SerializeField] private float reachRadius = 3f;

		private readonly List<LifeAgent> agents = new();
		private readonly List<LifeLabel> problemLabels = new();
		private GameObject labelPrefab;

		private void Start()
		{
			labelPrefab = Resources.Load<GameObject>("Life/LifeLabel");
			foreach (LifeAgent agent in FindObjectsByType<LifeAgent>(FindObjectsSortMode.None))
			{
				agents.Add(agent);
				problemLabels.Add(CreateProblemLabel(agent));
			}
		}

		// 매 프레임 ⚠ 라벨 위치·표시 갱신 — 문제 있는 주민만 "도와줘!"가 머리 위에 뜬다.
		private void Update()
		{
			for (int index = 0; index < agents.Count; index++)
			{
				LifeAgent agent = agents[index];
				LifeLabel label = problemLabels[index];
				if (agent == null || label == null)
				{
					continue;
				}

				bool show = agent.HasProblem; // 결핍 있으면 "도와줘!" 표시(라벨은 주민 자식 → 위치 자동 추종).
				if (label.gameObject.activeSelf != show)
				{
					label.gameObject.SetActive(show);
				}
			}
		}

		/// <summary>한 주민을 도와줌 — 가장 급한 욕구를 채운다. 도울 게 있었으면 true(피드백 로그).</summary>
		public bool HelpResident(LifeAgent agent)
		{
			if (agent == null)
			{
				return false;
			}

			bool helped = agent.TryHelp(helpAmount);
			if (helped)
			{
				Debug.Log($"[Life] 4호가 {agent.name} 도와줌 — 욕구 채움.");
			}

			return helped;
		}

		/// <summary>부감 클릭(A) — 카메라·스크린좌표에서 레이를 쏴 맞은 주민을 도와줌. 입력 트리거가 호출.</summary>
		public bool TryHelpAtScreenPoint(Camera camera, Vector2 screenPoint)
		{
			if (camera == null)
			{
				return false;
			}

			Ray ray = camera.ScreenPointToRay(screenPoint);
			if (Physics.Raycast(ray, out RaycastHit hit) == false)
			{
				return false;
			}

			LifeAgent agent = hit.collider.GetComponentInParent<LifeAgent>();
			return HelpResident(agent);
		}

		/// <summary>4호 근접+키(B) — 기준 위치(4호) 반경 내 *문제 있는* 가장 가까운 주민을 도와줌. 입력 트리거가 호출.</summary>
		public bool HelpNearest(Vector3 from)
		{
			LifeAgent best = null;
			float bestDistance = reachRadius;
			foreach (LifeAgent agent in agents)
			{
				if (agent == null || agent.HasProblem == false)
				{
					continue;
				}

				float distance = Vector3.Distance(from, agent.transform.position);
				if (distance <= bestDistance)
				{
					bestDistance = distance;
					best = agent;
				}
			}

			return HelpResident(best);
		}

		private LifeLabel CreateProblemLabel(LifeAgent agent)
		{
			if (labelPrefab == null)
			{
				return null;
			}

			GameObject go = Object.Instantiate(labelPrefab, agent.transform);
			go.transform.localPosition = new Vector3(0f, 2.1f, 0f);
			LifeLabel label = go.GetComponent<LifeLabel>();
			if (label != null)
			{
				label.SetStaticText("도와줘!");
				label.SetColor(new Color(1f, 0.3f, 0.25f)); // 빨강 — 문제 신호.
			}

			go.SetActive(false); // 문제 생기면 Update 가 켠다.
			return label;
		}
	}
}
