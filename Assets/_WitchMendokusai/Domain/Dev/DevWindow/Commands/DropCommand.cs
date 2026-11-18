using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// drop &lt;heal|magnet|freeze|clearscreen|nyang|exp|chest&gt; [count]
	/// 주인공 앞에 바닥 아이템을 옆으로 나란히. 냥 동전 값은 진열장 설정의 냥 칸 값.
	/// </summary>
	public class DropCommand : IDevCommand
	{
		public string Name => "drop";
		public string Usage => "drop <heal|magnet|freeze|clearscreen|nyang|exp|chest> [count]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}
			if (DevCommandArgs.TryEnum(args[0], out DevShowcaseDropType type) == false)
			{
				context.LogError($"모르는 아이템: '{args[0]}'");
				return;
			}
			int count = 1;
			if (args.Length > 1 && (int.TryParse(args[1], out count) == false || count < 1))
			{
				context.LogError($"개수는 1 이상 정수: '{args[1]}'");
				return;
			}

			DevWindowController controller = DevWindowController.Instance;
			DevShowcaseSO data = controller.Showcase.Data;
			Transform player = PlayerProvider.Instance.CurrentObject.transform;
			Vector3 start = player.position + player.forward * data.DropDistance - player.right * (count - 1) * data.DropSpacing / 2f;
			int nyangAmount = type == DevShowcaseDropType.Nyang ? NyangAmount(data) : 0;
			for (int i = 0; i < count; i++)
				controller.Drops.Spawn(type, start + player.right * i * data.DropSpacing, nyangAmount);
			context.LogSuccess($"{type} x{count}");
		}

		private static int NyangAmount(DevShowcaseSO data)
		{
			DevShowcaseDropCell nyang = data.Drops.FirstOrDefault(cell => cell.Type == DevShowcaseDropType.Nyang);
			if (nyang == null)
				throw new System.InvalidOperationException($"{nameof(DevShowcaseSO)} 에 냥 칸 없음. 동전 값 정본");
			return nyang.NyangAmount;
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<DevShowcaseDropType>(), partial[0]);
		}
	}
}
