using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-7 — 한 캐릭터의 자율 삶 성격을 데이터로 외부화한 ScriptableObject(수치노출 룰 정본).
	/// 욕구별 감소 곡선·문제 임계 + 자가회복 + 노동 성격(TASK-WM-183) = 캐릭터마다 다름(욘=Social 느림, 미식가=Hunger 빠름, 일꾼=Mine 잘함…).
	/// 하드코딩(LifeDirector.BuildDefaultProfile)을 대체 — 새 캐릭터 추가 = SO 1개(코드 0), UGC 입주도 이 경로.
	///
	/// 격상 순서(architecture.md): SO → 순수 NeedProfile/WorkProfile(<see cref="ToNeedProfile"/>·<see cref="ToWorkProfile"/>).
	/// SO 는 authoring, 런타임/모델은 순수 타입만 본다(Unity 의존 0의 DomainSDK 보호).
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

		// Unity 직렬화용 노동 효율 한 줄 — 이 캐릭터가 어떤 일을 얼마나 잘하나(미지정 일 = 기본 1.0).
		[System.Serializable]
		public struct WorkTuning
		{
			public WorkKind Kind;
			[Tooltip("효율 배수(1.0=기본). 클수록 그 일을 잘함 — 이 캐릭터의 특기.")]
			public float Efficiency;
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
		[SerializeField, Tooltip("개입 없을 때 자율로 하는 일(노동 성격) — 일꾼=Mine, 미식가=Cultivate…")]
		private WorkKind defaultWork = WorkKind.Forage;
		[SerializeField, Tooltip("일별 효율 배수(미지정 일은 기본 1.0). 이 캐릭터의 특기를 1.0 초과로.")]
		private WorkTuning[] works = new WorkTuning[0];

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

		/// <summary>authoring 노동 성격 → 순수 WorkProfile. 미지정 일 효율은 WorkProfile 이 기본 1.0 으로 채운다.</summary>
		public WorkProfile ToWorkProfile()
		{
			Dictionary<WorkKind, float> efficiencies = new(works.Length);
			foreach (WorkTuning tuning in works)
			{
				efficiencies[tuning.Kind] = tuning.Efficiency;
			}

			return new WorkProfile(defaultWork, efficiencies);
		}
	}
}
