using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 분류 하나. 도감 왼쪽 분류 판의 한 줄 (이름, 아이콘은 Sprite) 과 그 분류의 빈 칸 메달.
	/// 메달 그림은 업적마다 다름 (AchievementSO.TierMedals)
	/// </summary>
	[CreateAssetMenu(fileName = "ACC_", menuName = "WM/Achievement/" + nameof(AchievementCategorySO))]
	public class AchievementCategorySO : DataSO
	{
		[field: Header("_" + nameof(AchievementCategorySO))]
		[PropertyOrder(100)][field: SerializeField, Tooltip("분류 판 순서. 작은 것이 먼저")] public int Order { get; private set; }
		[PropertyOrder(101)][field: SerializeField, Tooltip("아직 못 모은 칸과 숨김 업적의 메달 자리")] public Sprite EmptySlot { get; private set; }
	}
}
