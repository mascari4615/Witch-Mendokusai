using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-7 — 한 캐릭터의 자율 삶 성격을 데이터로 외부화한 ScriptableObject(수치노출 룰 정본).
	/// 욕구별 감소 곡선·문제 임계 + 자가회복 속도 = 캐릭터마다 다름(욘=Social 느림, 미식가=Hunger 빠름…).
	/// 하드코딩(LifeDirector.BuildDefaultProfile)을 대체 — 새 캐릭터 추가 = SO 1개(코드 0), UGC 입주도 이 경로.
	///
	/// 격상 순서(architecture.md): SO → 순수 NeedProfile(<see cref="ToNeedProfile"/>). SO 는 authoring,
	/// 런타임/모델은 순수 타입만 본다(Unity 의존 0의 DomainSDK 보호).
	/// </summary>
	[CreateAssetMenu(fileName = "LifeProfile", menuName = "WM/Life/Life Profile")]
	public sealed class LifeProfileSO : ScriptableObject
	{
		// Unity 직렬화용 욕구 한 줄(NeedSpec 은 readonly struct 라 직접 직렬화 X → authoring 구조체로 입력).
		[System.Serializable]
		public struct NeedTuning
		{
			public NeedKind Kind;
			[Tooltip("분당 감소(게임-시간). 클수록 빨리 결핍 — 이 캐릭터가 자주 신경쓰는 욕구.")]
			public float DecayPerMinute;
			[Tooltip("이 값 미만이면 문제 상태(활동을 끌어당김).")]
			public float LowThreshold;
			[Tooltip("충족 상한.")]
			public float Max;
		}

		[SerializeField] private string displayName = "주민";
		[SerializeField] private NeedTuning[] needs =
		{
			new() { Kind = NeedKind.Hunger, DecayPerMinute = 0.13f, LowThreshold = 40f, Max = 100f },
			new() { Kind = NeedKind.Energy, DecayPerMinute = 0.11f, LowThreshold = 40f, Max = 100f },
			new() { Kind = NeedKind.Mood, DecayPerMinute = 0.09f, LowThreshold = 40f, Max = 100f },
			new() { Kind = NeedKind.Social, DecayPerMinute = 0.08f, LowThreshold = 40f, Max = 100f },
		};
		[SerializeField, Tooltip("지금 하는 활동이 그 욕구를 분당 채우는 양(자가회복). 소진보다 커야 활동이 한동안 지속.")]
		private float selfSatisfyPerMinute = 0.8f;

		public string DisplayName => displayName;
		public float SelfSatisfyPerMinute => selfSatisfyPerMinute;

		/// <summary>authoring 욕구 → 순수 NeedProfile(모델 입력). 등록 안 된 욕구는 빠진다(FastFail 정합).</summary>
		public NeedProfile ToNeedProfile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new(needs.Length);
			foreach (NeedTuning tuning in needs)
			{
				specs[tuning.Kind] = new NeedSpec(tuning.DecayPerMinute, tuning.LowThreshold, tuning.Max);
			}

			return new NeedProfile(specs);
		}
	}
}
