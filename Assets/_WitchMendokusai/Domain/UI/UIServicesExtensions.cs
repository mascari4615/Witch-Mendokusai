using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// VisualElement → panel-root 에 owner-push 된 UIServices 획득 (TASK-WM-133).
	/// 조상 walk 로 userData 가 UIServices 인 첫 노드 반환 — FilterBar 등
	/// userData=다른타입 노드는 타입 분기로 통과. panel 부착 후 유효.
	/// WM.Domain 거주 — Domain→Core 단방향이라 Domain 의 UIServices 를 참조하는
	/// 본 확장은 Domain asmdef 에 둔다(Core 배치 시 CS0246, 증분 1 검증으로 확정).
	/// </summary>
	public static class UIServicesExtensions
	{
		public static UIServices GetUIServices(this VisualElement element)
		{
			for (VisualElement current = element; current != null; current = current.parent)
			{
				if (current.userData is UIServices services)
					return services;
			}
			return null;
		}
	}
}
