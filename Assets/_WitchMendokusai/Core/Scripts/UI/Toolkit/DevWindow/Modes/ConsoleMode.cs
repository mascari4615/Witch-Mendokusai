using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 콘솔 모드. 명령행은 윈도우 하단에 항상 살아 있으므로 메인 영역은 도움말 텍스트.
	/// 향후 필터 UI / 확대 로그 등 추가 가치 검토 (Phase 1 미포함).
	/// </summary>
	public class ConsoleMode : IDevMode
	{
		public string Id => "console";
		public string DisplayName => "Console";
		public VisualElement Root { get; }

		public ConsoleMode()
		{
			Root = new VisualElement();
			Root.AddToClassList("wm-dev-mode-console");

			Label info = new("개발자 콘솔. 내장 명령은 'help' 로 확인하세요.");
			info.AddToClassList("wm-dev-mode-console__info");
			Root.Add(info);
		}

		public void OnActivate() { }
		public void OnDeactivate() { }
	}
}
