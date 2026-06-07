using System;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.DomainSDK.UGC
{
	/// <summary>
	/// 마도서 페이지가 요구하는 효과 공간 목표 좌표 + 허용 반경 = EffectTarget 의 JSON 직렬화 표면.
	/// BrewVector 가 nested object(positionX/Y)를 갖는 대신 평탄 float 필드로 풀어, 팬 JSON 손수 작성 진입장벽 0.
	/// ToEffectTarget() = Domain 측 변환 책임 — DomainSDK 안에서 정합 유지.
	/// </summary>
	[Serializable]
	public class UGCRecipeTargetData
	{
		public float positionX;
		public float positionY;
		public float radius;

		public EffectTarget ToEffectTarget()
		{
			return new EffectTarget
			{
				Position = new BrewVector(positionX, positionY),
				Radius = radius,
			};
		}
	}
}
