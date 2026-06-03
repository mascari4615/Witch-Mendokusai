using TMPro;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-5d — 카메라를 향하는 월드 라벨(갈무리 한글). 두 쓰임:
	/// ① 캐릭터 머리 위 = 부모 <see cref="LifeAgent"/> 의 현재 활동을 "먹는 중/자는 중…"으로 표시(자동 갱신).
	/// ② 장소 패드 = 고정 텍스트("식당" 등) — <see cref="SetStaticText"/> 로 박고 부모 LifeAgent 없으면 그대로 유지.
	/// 프리팹(Resources/Life/LifeLabel)에 TMP+갈무리 폰트가 직렬화돼 런타임 로드. 빌보드는 LateUpdate.
	/// </summary>
	[RequireComponent(typeof(TMP_Text))]
	public class LifeLabel : MonoBehaviour
	{
		private TMP_Text label;
		private LifeAgent agent;
		private bool staticText;

		private void Awake() => label = GetComponent<TMP_Text>();

		private void Start()
		{
			if (staticText)
			{
				return; // 장소 라벨 — 고정 텍스트 유지.
			}

			agent = GetComponentInParent<LifeAgent>();
			if (agent != null)
			{
				Apply(agent.CurrentActivity);
				agent.OnActivityChanged += Apply;
			}
		}

		private void OnDestroy()
		{
			if (agent != null)
			{
				agent.OnActivityChanged -= Apply;
			}
		}

		/// <summary>장소 라벨로 고정(부모 LifeAgent 바인딩 안 함).</summary>
		public void SetStaticText(string text)
		{
			if (label == null)
			{
				label = GetComponent<TMP_Text>();
			}

			staticText = true;
			label.text = text;
		}

		private void Apply(ActivityKind activity)
		{
			if (label != null)
			{
				label.text = StatusText(activity);
			}
		}

		// 카메라를 향하도록(빌보드) — 어느 각도서 봐도 글자가 읽히게.
		private void LateUpdate()
		{
			Camera camera = Camera.main;
			if (camera == null)
			{
				return;
			}

			transform.forward = (transform.position - camera.transform.position).normalized;
		}

		// 활동 → 사람이 읽는 한 마디. Idle = 쉬는 중.
		private static string StatusText(ActivityKind activity) => activity switch
		{
			ActivityKind.Eat => "먹는 중",
			ActivityKind.Sleep => "자는 중",
			ActivityKind.Hobby => "노는 중",
			ActivityKind.Socialize => "어울리는 중",
			_ => "쉬는 중",
		};
	}
}
