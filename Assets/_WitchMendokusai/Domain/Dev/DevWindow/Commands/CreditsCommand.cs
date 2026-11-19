using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// credits , 설정창 크레딧 탭을 바로 엶. 개발 창은 닫음
	/// </summary>
	public class CreditsCommand : IDevCommand
	{
		public string Name => "credits";
		public string Usage => "credits";

		public void Execute(DevCommandContext context, string[] args)
		{
			DevWindowController.Instance.CloseWindow();
			UIRoot.Instance.SettingView.OpenCredits();
			context.LogSuccess("크레딧 탭 열림");
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
