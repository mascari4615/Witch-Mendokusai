using System.Collections.Generic;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	/// <summary>
	/// 새 게임의 저장 자료. 빈 GameData 에 처음부터 다른 값만 채움
	/// 여기 없는 칸은 GameData 의 기본값이고 불러올 때 빈 상태가 됨. 저장 칸을 더해도 여기를 고칠 일이 없음
	/// </summary>
	public static class NewGameData
	{
		public const int STARTING_NYANG = 1000;

		/// <summary>windowLayouts 는 진행이 아니라 화면 취향이라 새 게임에도 남김</summary>
		public static GameData Create(List<WindowLayoutEntry> windowLayouts)
		{
			GameData newGame = new()
			{
				saveVersion = SaveManager.SAVE_VERSION,
				windowLayouts = windowLayouts,
				// 옛 저장의 빈 칸 (null) 과 구분되게 빈 목록
				dialogueHistory = new DialogueHistory().ToSaveData(),
			};

			newGame.gameStats[GameStatType.NYANG] = STARTING_NYANG;
			ForEach<Doll>(doll => newGame.dolls.Add(doll.CreateDefaultSaveData()));
			ForEach<ItemData>(itemData => newGame.hasRecipe[itemData.ID] = false);
			ForEach<QuestSO>(questData => newGame.questStates[questData.ID] = (int)QuestState.Locked);
			return newGame;
		}
	}
}
