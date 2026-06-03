using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-8 — 자율 삶의 *관계* 레이어를 라이브로 켠다(INC-3 RelationshipModel 배선).
	/// 주민쌍이 *가까이 함께 보낸 시간*만큼 친밀도가 쌓이고(특히 둘 다 수다터서 Socialize 면 더), 자율로
	/// 아는사이→친구→단짝→동거까지 승급(AutoCeiling). 연애·결혼은 자율 승급 X — 4호 개입 전용(<see cref="TryPairBond"/>).
	///
	/// 가시화: 친한 쌍 사이 선(가까울수록 따뜻한 색·굵게) + 중간 한글 라벨("친구"/"동거"…). "둘이 친해졌다"가 눈에.
	/// 미래(INC-7): 친밀도/임계가 LifeProfileSO 로 외부화. 지금은 공통 코드 디폴트(수치노출).
	/// </summary>
	public class LifeRelationshipDirector : MonoBehaviour
	{
		// 친밀도가 쌓이는 거리 — 이 안에 함께 있으면 가까워진다. 수치노출.
		[SerializeField] private float socialRadius = 4f;
		// 가까이 있을 때 초당 친밀도 증가. 수치노출 — 관계 진전 속도 손잡이.
		[SerializeField] private float proximityAffinityPerSecond = 2f;
		// 둘 다 Socialize(수다터) 일 때 추가 초당 친밀도. "같이 논다"가 관계의 핵심 입력.
		[SerializeField] private float socializeBonusPerSecond = 4f;
		// 선이 보이기 시작하는 최소 단계(Stranger 는 선 X).
		[SerializeField] private float lineWidth = 0.12f;

		private readonly List<Bond> bonds = new();
		private RelationshipParams parameters;
		private Material lineMaterial;
		private GameObject labelPrefab;

		// init-order-ok: 씬 정적 LifeAgent 들을 Start 에서 1회 수집해 모든 쌍의 관계를 만든다.
		private void Start()
		{
			parameters = BuildDefaultParams();
			lineMaterial = new Material(Shader.Find("Sprites/Default"));
			labelPrefab = Resources.Load<GameObject>("Life/LifeLabel"); // 관계 단계 한글 라벨(없으면 선만).

			LifeAgent[] agents = FindObjectsByType<LifeAgent>(FindObjectsSortMode.None);
			for (int a = 0; a < agents.Length; a++)
			{
				for (int b = a + 1; b < agents.Length; b++)
				{
					bonds.Add(CreateBond(agents[a], a, agents[b], b));
				}
			}
		}

		private void OnDestroy()
		{
			if (lineMaterial != null)
			{
				Destroy(lineMaterial);
			}
		}

		private void Update()
		{
			foreach (Bond bond in bonds)
			{
				if (bond.A == null || bond.B == null)
				{
					continue;
				}

				Vector3 pa = bond.A.transform.position;
				Vector3 pb = bond.B.transform.position;
				float distance = Vector3.Distance(pa, pb);

				if (distance <= socialRadius)
				{
					// 함께 보낸 시간만큼 친밀(둘 다 수다 중이면 더). 자율 승급은 동거까지(모델이 ceiling 보장).
					float gain = proximityAffinityPerSecond;
					if (bond.A.CurrentActivity == ActivityKind.Socialize && bond.B.CurrentActivity == ActivityKind.Socialize)
					{
						gain += socializeBonusPerSecond;
					}

					RelationshipStage before = bond.State.Stage;
					RelationshipModel.AddAffinity(bond.State, parameters, gain * Time.deltaTime);
					if (bond.State.Stage != before)
					{
						Debug.Log($"[Life] {bond.A.name} ↔ {bond.B.name} = {StageText(bond.State.Stage)}");
						UpdateBondLabelText(bond);
					}
				}

				UpdateBondVisual(bond, pa, pb);
			}
		}

		private Bond CreateBond(LifeAgent a, int idA, LifeAgent b, int idB)
		{
			GameObject lineGo = new($"관계 [{a.name} ↔ {b.name}]");
			lineGo.transform.SetParent(transform);
			LineRenderer line = lineGo.AddComponent<LineRenderer>();
			line.material = lineMaterial;
			line.positionCount = 2;
			line.numCapVertices = 4;
			line.enabled = false; // Stranger = 안 보임.

			LifeLabel label = null;
			if (labelPrefab != null)
			{
				GameObject labelGo = Object.Instantiate(labelPrefab, transform);
				label = labelGo.GetComponent<LifeLabel>();
			}

			return new Bond
			{
				A = a,
				B = b,
				State = new RelationshipState(idA, idB),
				Line = line,
				Label = label,
			};
		}

		// 선/라벨 갱신 — 단계 ≥ 아는사이면 선을 켜고 단계 색·굵기, 중간에 한글 라벨.
		private void UpdateBondVisual(Bond bond, Vector3 pa, Vector3 pb)
		{
			bool visible = (int)bond.State.Stage >= (int)RelationshipStage.Acquaintance;
			bond.Line.enabled = visible;
			if (bond.Label != null)
			{
				bond.Label.gameObject.SetActive(visible);
			}

			if (visible == false)
			{
				return;
			}

			Vector3 lift = new(0f, 0.5f, 0f); // 바닥보다 살짝 위로 그어 보이게.
			bond.Line.SetPosition(0, pa + lift);
			bond.Line.SetPosition(1, pb + lift);

			Color color = StageColor(bond.State.Stage);
			bond.Line.startColor = color;
			bond.Line.endColor = color;
			float width = lineWidth * StageWidthScale(bond.State.Stage);
			bond.Line.startWidth = width;
			bond.Line.endWidth = width;

			if (bond.Label != null)
			{
				bond.Label.transform.position = Vector3.Lerp(pa, pb, 0.5f) + new Vector3(0f, 1f, 0f); // 두 주민 중간 위.
			}
		}

		private static void UpdateBondLabelText(Bond bond)
		{
			if (bond.Label != null)
			{
				bond.Label.SetStaticText(StageText(bond.State.Stage));
			}
		}

		// 단계별 한글 — 라벨·로그 공용.
		private static string StageText(RelationshipStage stage) => stage switch
		{
			RelationshipStage.Acquaintance => "아는 사이",
			RelationshipStage.Friend => "친구",
			RelationshipStage.BestFriend => "단짝",
			RelationshipStage.Housemate => "동거",
			RelationshipStage.Partner => "연인",
			RelationshipStage.Married => "부부",
			_ => "남",
		};

		// 단계 깊어질수록 따뜻한 색(회색→노랑→주황→분홍→빨강).
		private static Color StageColor(RelationshipStage stage) => stage switch
		{
			RelationshipStage.Acquaintance => new Color(0.6f, 0.6f, 0.6f),
			RelationshipStage.Friend => new Color(0.95f, 0.85f, 0.3f),
			RelationshipStage.BestFriend => new Color(1f, 0.6f, 0.2f),
			RelationshipStage.Housemate => new Color(0.95f, 0.4f, 0.6f),
			RelationshipStage.Partner => new Color(1f, 0.3f, 0.4f),
			RelationshipStage.Married => new Color(1f, 0.1f, 0.2f),
			_ => Color.gray,
		};

		// 단계 깊을수록 선 굵게.
		private static float StageWidthScale(RelationshipStage stage) => 1f + 0.4f * (int)stage;

		// 디폴트 관계 튜닝 — 단계별 진입 친밀도 + 자율 상한(동거). 수치노출. INC-7 에서 SO 로 외부화.
		private static RelationshipParams BuildDefaultParams()
		{
			float[] entry = { 0f, 20f, 60f, 120f, 200f, 320f, 460f }; // Stranger..Married 누적 친밀도.
			return new RelationshipParams(entry, RelationshipStage.Housemate);
		}

		private sealed class Bond
		{
			public LifeAgent A;
			public LifeAgent B;
			public RelationshipState State;
			public LineRenderer Line;
			public LifeLabel Label;
		}
	}
}
