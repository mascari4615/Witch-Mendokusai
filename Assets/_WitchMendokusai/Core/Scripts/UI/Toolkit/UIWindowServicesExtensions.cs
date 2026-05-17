using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// VisualElement → panel-root owner-push 된 IUIWindowServices 획득
	/// (TASK-WM-133 증분 2). 조상 walk 로 userData 가 IUIWindowServices 인 첫
	/// 노드 반환 — Domain 의 UIServices 가 그 인터페이스를 구현. WM.Core 거주
	/// (WMWindow 가 Core 라 Domain UIServices 직접 참조 불가, 인터페이스 경유).
	/// panel 부착 후 유효.
	/// </summary>
	public static class UIWindowServicesExtensions
	{
		public static IUIWindowServices GetUIWindowServices(this VisualElement element)
		{
			for (VisualElement current = element; current != null; current = current.parent)
			{
				if (current.userData is IUIWindowServices services)
					return services;
			}
			return null;
		}
	}
}
