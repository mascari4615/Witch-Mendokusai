using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.UGC
{
	/// <summary>
	/// 팬이 마도서 한 페이지에 적는 레시피 정의 = 데이터 주도 UGC 표면.
	/// BrewRecipe(Id/EffectName/Target) 정합 + 팬 작성 메타(표제/설명/태그) + 권장 재료·등급 임계.
	/// UnityEngine·Newtonsoft 의존 0 — DomainSDK references=[UniTask, MessagePipe] 정합. Domain 측 loader/validator 가 직렬화 담당.
	/// 본격 진입 시 RecipeSO 가 감싸 디자이너 노출 + 마도서 페이지 UI/G6 모드 등록 표면.
	/// </summary>
	[Serializable]
	public class UGCRecipePageData
	{
		public int recipeId;
		public string effectName;
		public string title;
		public string description;
		public UGCRecipeTargetData target;
		public List<UGCRecipeIngredientRefData> ingredients = new();
		public UGCRecipeGradeThresholdsData gradeThresholds;
		public List<string> tags = new();
	}
}
