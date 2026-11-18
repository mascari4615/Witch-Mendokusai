using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "DC_", menuName = "WM/Variable/" + nameof(DungeonConstraint))]
	public class DungeonConstraint : DataSO
	{
		[field: SerializeField] public List<DungeonConstraintEffectInfo> Effects { get; private set; } = new();

		[Tooltip("위험도. 켠 제약의 합이 입장 창 위험도 합계. 입장 창 칸 모서리 숫자")]
		[field: SerializeField, Range(1, 3)] public int Risk { get; private set; } = 1;

		[Tooltip("입장 창 칩의 수치 단위. 예: %")]
		[field: SerializeField] public string ValueUnit { get; private set; } = string.Empty;

		/// <summary> 칩 글. 첫 효과 수치와 단위 </summary>
		public string ShortValue => Effects.Count == 0 ? string.Empty : (Effects[0].Value >= 0 ? "+" : string.Empty) + Effects[0].Value + ValueUnit;
	}
}
