using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// chest , 상자 보상을 바로 엶 (주운 것과 같은 경로). 바닥에 상자를 두려면 drop chest. 판 안에서만.
	/// </summary>
	public class ChestCommand : IDevCommand
	{
		public string Name => "chest";
		public string Usage => "chest";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			EventBusBridge.Publish(new ChestPickedUpEvent { Position = PlayerProvider.Instance.CurrentObject.transform.position });
			context.LogSuccess("상자 개봉");
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
