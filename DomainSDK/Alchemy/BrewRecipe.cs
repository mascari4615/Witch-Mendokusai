using System;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 마도서 페이지/레시피 = 데이터 주도. "이 효과 좌표에 도달하면 이 포션 완성"의 목표 정의.
    /// "커스텀 쉽게" — 새 페이지/효과 = 코드 변경 0, 이 POCO(후속 RecipeSO/마도서 페이지 SO) 추가만.
    /// 직렬화 POCO(UnityEngine 의존 0) → 후속 SO 가 감싸 디자이너 노출 + 팬 UGC 공유 표면.
    /// Target = 효과 공간 목표 좌표 + 허용 반경(난이도 = 반경 축소). EffectName 은 placeholder 라벨(디자인 미확정).
    /// </summary>
    [Serializable]
    public struct BrewRecipe
    {
        public int Id;
        public string EffectName;
        public EffectTarget Target;
    }
}
