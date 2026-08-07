using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Refining;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-172 × TASK-WM-170 — 정련 단계를 거친 상품의 <b>값</b>을 정한다.
	///
	/// 정련 모델은 「품질」과 「온기」 두 값을 낸다. 여기서는 <b>품질만</b> 값에 반영한다.
	///
	/// ★ 온기를 일부러 안 쓴다: 온기는 「함부로 하느냐 애도하며 하느냐」의 눈금이고,
	///   그게 게임에서 <b>얼마나 큰 보상·벌이 되는지는 사용자가 정할 문제</b>라고 설계 문서가 못 박아 뒀다
	///   (TASK-WM-172 § 사용자 컨펌 대기). 값에 몰래 섞으면 그 결정을 코드가 먼저 해 버리는 것이 된다.
	///   그래서 계산해서 <b>보여만 주고</b>, 값에는 안 넣는다.
	/// </summary>
	public static class WorkshopRefinedPrice
	{
		/// <summary>
		/// 기본가 × (1 + 품질보정 × 품질). 품질은 0~1 이라, 보정이 1 이면 최대 두 배까지 간다.
		/// 정련 단계가 없으면 기본가 그대로. 결과는 내림(정수 골드 — 소수 가격은 결정성이 깨진다).
		/// </summary>
		public static int Evaluate(int basePrice, IReadOnlyList<RefiningStage> stages, RefiningCoefficients coefficients, float qualityPriceBonus)
		{
			if (stages == null || stages.Count == 0)
			{
				return basePrice;
			}

			RefiningState state = RefiningChain.Evaluate(stages, coefficients);
			float scaled = basePrice * (1f + qualityPriceBonus * state.Quality);

			return Mathf.Max(0, Mathf.FloorToInt(scaled));
		}

		/// <summary>정련 결과 그 자체 — 온기까지 들어 있다. 이야기·연출이 읽을 자리다(값 계산엔 안 쓴다).</summary>
		public static RefiningState Outcome(IReadOnlyList<RefiningStage> stages, RefiningCoefficients coefficients)
		{
			if (stages == null || stages.Count == 0)
			{
				return RefiningChain.Initial(coefficients);
			}

			return RefiningChain.Evaluate(stages, coefficients);
		}
	}
}
