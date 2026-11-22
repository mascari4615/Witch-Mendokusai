using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 분류 하나. 분류마다 모으는 물건 (도장, 자수 패치, 물약병 ...) 이 다르고, 칸 그림은 분류가 들고 있음.
	/// 업적 창은 분류 탭을 넘길 때 이 자료만 바꿔 끼움. 그림이 아직 없으면 MotifKey 의 USS 임시 모양
	/// </summary>
	[CreateAssetMenu(fileName = "ACC_", menuName = "WM/Achievement/" + nameof(AchievementCategorySO))]
	public class AchievementCategorySO : DataSO
	{
		[field: Header("_" + nameof(AchievementCategorySO))]
		[PropertyOrder(100)][field: SerializeField, Tooltip("탭 순서. 작은 것이 먼저")] public int Order { get; private set; }
		[PropertyOrder(101)][field: SerializeField, Tooltip("모으는 물건 이름. 머리줄에 쓰임")] public string MotifName { get; private set; }
		[PropertyOrder(102)][field: SerializeField, Tooltip("임시 모양 USS 열쇠 (stamp, patch, bottle). 칸에 wm-ach-slot--열쇠 로 붙음")] public string MotifKey { get; private set; }
		[PropertyOrder(103)][field: SerializeField, Tooltip("단계별 칸 틀 그림. 1단계부터. 비면 USS 임시 모양")] public List<Sprite> TierFrames { get; private set; } = new();
		[PropertyOrder(104)][field: SerializeField, Tooltip("아직 못 모은 칸")] public Sprite EmptySlot { get; private set; }
		[PropertyOrder(105)][field: SerializeField, Tooltip("숨김 업적의 못 모은 칸")] public Sprite HiddenSlot { get; private set; }

		/// <summary>단계 틀 그림. 0단계나 그림이 모자라면 null (USS 임시 모양)</summary>
		public Sprite FrameOf(int tier)
		{
			if (tier <= 0 || tier > TierFrames.Count)
			{
				return null;
			}
			return TierFrames[tier - 1];
		}
	}
}
