using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// sigcard &lt;cardId&gt; [cardId...] , 전용 무기 카드를 조건 없이 바로 받음. 형태 카드면 숙련 조각을 채워 변신까지. 판 안에서만.
	/// </summary>
	public class SignatureCardCommand : IDevCommand
	{
		public string Name => "sigcard";
		public string Usage => "sigcard <cardId> [cardId...]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			CardManager cardManager = Object.FindAnyObjectByType<CardManager>();
			foreach (string arg in args)
			{
				if (int.TryParse(arg, out int cardId) == false || cardManager.TryGrantSignatureCard(cardId, out CardData card) == false)
				{
					context.LogError($"이 인물의 전용 무기 카드가 아니거나 더 받을 수 없음: '{arg}'");
					return;
				}
				context.LogSuccess($"{card.ID} {card.Name}");
			}
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
