using System;

namespace WitchMendokusai.DomainSDK.UGC
{
	/// <summary>
	/// 레시피 페이지가 권장하는 재료 1개 참조 = BrewIngredient.Id + 기본 갈기량 표면.
	/// 본격 진입 시 IngredientSO 가 등록한 ID 와 매칭(미등록 ID = sandbox reject).
	/// 팬이 자기 레시피에 "이 재료 이만큼 갈아라" 라고 적는 표면.
	/// </summary>
	[Serializable]
	public class UGCRecipeIngredientRefData
	{
		public int ingredientId;
		public float grind;
	}
}
