using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// showcase , 개발자 진열장 판으로 이동. 상태이상, 몬스터, 부서지는 물건, 바닥 아이템, 무기 시험대.
	/// </summary>
	public class ShowcaseCommand : IDevCommand
	{
		public string Name => "showcase";
		public string Usage => "showcase";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DungeonManagerBridge.IsDungeon)
			{
				context.LogError("이미 판 안. 판을 끝낸 뒤 들어가기");
				return;
			}
			DevWindowController.Instance.Showcase.Enter();
			// 전환 중이면 DungeonManager 가 조용히 무시하므로 요청까지만 보고
			context.LogSuccess("진열장 이동 요청");
			DevWindowController.Instance.CloseWindow();
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
