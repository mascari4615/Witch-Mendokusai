using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 던전 제약 하나. 추가와 삭제는 에셋 만들고 던전 Constraints 목록에 넣고 빼기만
	/// 같은 Family 의 제약은 단계 (Level) 만 다른 같은 제약. 한 번에 하나만 켜짐
	/// 위험도는 켠 제약 Level 의 합 (DungeonConstraintRules)
	/// </summary>
	[CreateAssetMenu(fileName = "DC_", menuName = "WM/Variable/" + nameof(DungeonConstraint))]
	public class DungeonConstraint : DataSO
	{
		[field: SerializeField] public List<DungeonConstraintEffectInfo> Effects { get; private set; } = new();

		[Tooltip("같은 제약 묶음 이름. 같은 이름끼리 한 칸 열, 하나만 켜짐. 비우면 혼자 한 열")]
		[field: SerializeField] public string Family { get; private set; } = string.Empty;

		[Tooltip("단계 1~3. 위험도에 이 값만큼 더함. 설정 창 줄")]
		[field: SerializeField, Range(1, 3)] public int Level { get; private set; } = 1;

		[Tooltip("입장 창 칩의 수치 단위. 예: %")]
		[field: SerializeField] public string ValueUnit { get; private set; } = string.Empty;

		/// <summary> 열 묶음 키. Family 가 비면 에셋마다 다른 키 </summary>
		public string FamilyKey => string.IsNullOrEmpty(Family) ? "#" + ID : Family;

		/// <summary> 칩 글. 첫 효과 수치와 단위 </summary>
		public string ShortValue => Effects.Count == 0 ? string.Empty : (Effects[0].Value >= 0 ? "+" : string.Empty) + Effects[0].Value + ValueUnit;
	}
}
