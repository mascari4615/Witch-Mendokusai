using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 사이드바 모드 인터페이스. 명령 쪽(IDevCommand + DevCommandRegistry)과 대칭.
	/// 새 모드 추가 = 구현체 작성 + DevModeRegistry.Register 한 줄.
	/// </summary>
	public interface IDevMode
	{
		/// <summary>모드 식별자. 예: "console", "items"</summary>
		string Id { get; }

		/// <summary>사이드바에 표시될 라벨. 예: "Console", "Items"</summary>
		string DisplayName { get; }

		/// <summary>활성화 시 메인 영역에 attach될 루트.</summary>
		VisualElement Root { get; }

		/// <summary>모드 진입 시. 데이터 reload 등 lazy 초기화 hook.</summary>
		void OnActivate();

		/// <summary>모드 이탈 시. 정리 또는 상태 보존.</summary>
		void OnDeactivate();
	}
}
