using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Life
{
	/// <summary>
	/// 한 주민의 "노동 성격" — 자율로 하는 기본 일(<see cref="DefaultWork"/>) + 일별 효율 배수. 순수(DomainSDK).
	/// <see cref="NeedProfile"/> 의 형제 — LifeProfileSO 가 욕구 곡선과 함께 생성(INC-W4).
	/// 일꾼은 Mine 효율↑·DefaultWork=Mine, 미식가는 Cook/Cultivate … 식의 개성을 데이터로 담는다.
	///
	/// 효율 미지정 일 = 기본 1.0 — 누구나 어떤 일이든 할 수 있다(4호가 아무 일이나 시킬 수 있으니).
	/// 특화는 1.0 초과/미만으로 표현. "기본 숙련" 의미라 결핍 마스킹 아님(FastFail 대신 기본값이 정합).
	/// </summary>
	public sealed class WorkProfile
	{
		// 효율 미지정 일의 기본 숙련 — 특화 안 된 일도 기본 속도로 수행(타입 기본값, 튜닝값 아님).
		private const float DEFAULT_EFFICIENCY = 1f;

		private readonly Dictionary<WorkKind, float> efficiencyByKind;

		public WorkProfile(WorkKind defaultWork, IReadOnlyDictionary<WorkKind, float> efficiencies)
		{
			DefaultWork = defaultWork;
			efficiencyByKind = new Dictionary<WorkKind, float>(efficiencies.Count);

			foreach (KeyValuePair<WorkKind, float> entry in efficiencies)
			{
				efficiencyByKind[entry.Key] = entry.Value;
			}
		}

		/// <summary>개입 없을 때 자율로 하는 일.</summary>
		public WorkKind DefaultWork { get; }

		/// <summary>이 일의 효율 배수 — 미지정이면 기본 숙련 1.0(누구나 기본 속도).</summary>
		public float EfficiencyOf(WorkKind kind)
		{
			return efficiencyByKind.TryGetValue(kind, out float efficiency) ? efficiency : DEFAULT_EFFICIENCY;
		}
	}
}
