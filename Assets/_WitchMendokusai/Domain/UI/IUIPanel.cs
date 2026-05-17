using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// UIPanelGroup 가 패널에 의존하는 계약 (panel-kind 추상화).
	/// 레거시 uGUI UIPanel + 신 Toolkit 패널이 공통 구현 → 패널그룹이 둘을 동형 관리.
	/// 표면 = UIPanelGroup.Start/SetPanel + UIManager/UINPCMenu 가 실제 사용하는 멤버만 (TASK-WM-113 S1).
	/// </summary>
	public interface IUIPanel
	{
		string Name { get; }
		Sprite PanelIcon { get; }
		bool IsFullscreen { get; }

		void Init(IUIPanelGroup group);
		void SetActive(bool newActive);
		void SetNPC(NPCObject npc);
		void UpdateUI();
	}
}
