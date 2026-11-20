using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// reveal pack &lt;full|quick|chain|old&gt; [common|rare|epic] , 카드 공개 연출을 바로 실행. 판 안에서만.
	/// - 카드 고르기가 열려 있으면 그 카드로 다시 공개. 아니면 레벨업을 열고 장비 고르기를 첫 칸으로 넘긴 뒤 공개
	/// - old 는 옛 팩 스킨 (비교용, 에디터에서만)
	/// reveal reset , 카드팩 풀 연출 본 횟수를 0 으로 (다음 공개가 풀 연출)
	/// </summary>
	public class RevealCommand : IDevCommand
	{
		private const float WAIT_SECONDS = 10f;
		private static readonly string[] MODES = { "full", "quick", "chain", "old" };
		private static readonly string[] TIERS = { "common", "rare", "epic" };

		public string Name => "reveal";
		public string Usage => "reveal pack <full|quick|chain|old> [common|rare|epic] | reveal reset";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length >= 1 && DevCommandArgs.Is(args[0], "reset"))
			{
				DataManager.Instance.GameStat[GameStatType.CARD_PACK_FULL_REVEAL_SEEN] = 0;
				context.LogSuccess("카드팩 풀 연출 본 횟수 0, 다음 공개는 풀 연출");
				return;
			}
			if (args.Length < 2 || DevCommandArgs.Is(args[0], "pack") == false)
			{
				context.LogError(Usage);
				return;
			}
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;

			CardManager cardManager = Object.FindAnyObjectByType<CardManager>();
			UISelectCard panel = Object.FindAnyObjectByType<UISelectCard>(FindObjectsInactive.Include);
			CardRevealSkin full = panel.Theme.Reveal.SelectCard;
			string mode = args[1].ToLowerInvariant();
			CardRevealSkin skin = mode switch
			{
				"full" => full,
				"quick" or "chain" => full.RepeatSkin != null ? full.RepeatSkin : full,
				"old" => FindOldSkin(),
				_ => null,
			};
			if (skin == null)
			{
				context.LogError(mode == "old" ? "옛 팩 스킨은 에디터에서만" : $"모르는 방식: '{args[1]}'");
				return;
			}
			CardRevealTier? best = null;
			if (args.Length >= 3)
			{
				int tierIndex = System.Array.FindIndex(TIERS, tier => DevCommandArgs.Is(args[2], tier));
				if (tierIndex < 0)
				{
					context.LogError($"모르는 등급: '{args[2]}'");
					return;
				}
				best = (CardRevealTier)tierIndex;
			}

			UISelectCard.DevRevealRequest request = new() { Skin = skin, Chained = mode == "chain", Best = best };
			if (cardManager.CurPanelType == CardPanelType.SelectCard)
			{
				panel.DevReplay(request);
				context.LogSuccess($"공개 다시: {skin.name} {(best.HasValue ? best.Value.ToString() : "실제 등급")}");
				return;
			}
			UISelectCard.DevQueue(request);
			if (cardManager.CurPanelType == CardPanelType.None)
			{
				UnitStat stat = PlayerProvider.Instance.Current.UnitStat;
				stat[UnitStatType.EXP_CUR] += stat[UnitStatType.EXP_MAX];
			}
			DevWindowController.Instance.StartCoroutine(PassDeckChoice(cardManager));
			context.LogSuccess($"레벨업 열고 공개 예약: {skin.name}");
		}

		// 장비 고르기가 뜨면 첫 칸을 골라 카드 고르기로 넘김
		private static IEnumerator PassDeckChoice(CardManager cardManager)
		{
			float waited = 0f;
			while (waited < WAIT_SECONDS)
			{
				if (cardManager.CurPanelType == CardPanelType.SelectCard)
					yield break;
				if (cardManager.CurPanelType == CardPanelType.SelectDeck)
				{
					cardManager.SelectDeck(0);
					yield break;
				}
				waited += Time.unscaledDeltaTime;
				yield return null;
			}
		}

		private static CardRevealSkin FindOldSkin()
		{
#if UNITY_EDITOR
			foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(PackOpenRevealSkin)))
				return UnityEditor.AssetDatabase.LoadAssetAtPath<PackOpenRevealSkin>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#endif
			return null;
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length <= 1)
				return new[] { "pack", "reset" };
			if (partial.Length == 2)
				return MODES;
			return partial.Length == 3 ? TIERS : Enumerable.Empty<string>();
		}
	}
}
