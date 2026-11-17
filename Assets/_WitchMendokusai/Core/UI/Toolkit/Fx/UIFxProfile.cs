using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 하나의 연출 묶음. 패널 (UIToolkitPanel.profile), 창 (WMWindow.Profile) 에 하나 끼우고, 비우면 UIFxSettings 기본 프로필
	/// 칸마다 에셋을 끼우고, 비운 칸은 그 연출을 끔. 같은 결의 화면은 같은 프로필을 나눠 씀
	/// 이름 규격 Profile_{ID}_{이름}.asset
	/// </summary>
	[CreateAssetMenu(fileName = "Profile_0_", menuName = "WM/UI/Fx Profile")]
	public sealed class UIFxProfile : ScriptableObject
	{
		[Header("열고 닫기")]
		[Tooltip("비우면 UIFxSettings 의 windowMotion, panelMotion USS 움직임")]
		public UIPanelMotion panelMotion;
		[Tooltip("하위 창이 열릴 때 부모가 뒤로 밀림")]
		public UIStackPush stackPush;
		[Tooltip("패널 뒤 살아 있는 배경 (전체 화면 패널)")]
		public UIBackdrop backdrop;

		[Header("가리키기")]
		public UICursorStyle cursor;
		public UISelectStyle select;
		public UIDimStyle dim;

		[Header("평상시")]
		public UIIdleEffect idleEffect;
		public UIAmbience ambience;
		public UIParallax parallax;
		[Tooltip("wm-text-wave 클래스 글자")]
		public UITextFx textFx;

		[Header("화면이 불러 쓰는 것")]
		public UICharacterMotion characterMotion;
		public UIRewardReveal rewardReveal;
		public UIValueChange valueChange;
	}
}
