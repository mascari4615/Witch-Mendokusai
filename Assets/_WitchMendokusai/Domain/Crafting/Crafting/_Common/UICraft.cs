using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UICraft : UIBase
	{
		[SerializeField] private ItemType itemType;
		[SerializeField] private RecipeType recipeType;

		[SerializeField] private TextMeshProUGUI percentageText;
		[SerializeField] private TextMeshProUGUI priceText;

		[SerializeField] private UIItemSlot[] craftTableSlots;
		[SerializeField] private TextMeshProUGUI[] craftTableAmounts;

		[SerializeField] private UIItemSlot[] resultSlots;
		[SerializeField] private TextMeshProUGUI[] resultAmounts;

		[SerializeField] private UIItemDataGrid recipeGrid;

		private UIManager uiManager;
		private SOManager soManager;
		private DataManager dataManager;

		[Inject]
		public void Construct(UIManager uiManager, SOManager soManager, DataManager dataManager)
		{
			this.uiManager = uiManager;
			this.soManager = soManager;
			this.dataManager = dataManager;
		}

		protected override void OnOpen()
		{
			// Debug.Log($"{nameof(UICraft)} {nameof(OnEnable)}");
			StartCoroutine(Loop());
		}

		protected override void OnClose()
		{
			// Debug.Log($"{nameof(UICraft)} {nameof(OnDisable)}");
			StopAllCoroutines();
		}

		/// <summary>세계가 돌려준 결과를 사람에게 보여 준다 — 조용히 끝나면 「고장」으로 읽힌다.</summary>
		private void ShowWorldCraftResult()
		{
			if (WorldCraftBridge.IsActive == false)
				return;

			if (WorldCraftBridge.Channel.TryTakeResult(out CraftResult result) == false)
				return;

			if (result.Attempted == false)
			{
				uiManager.PopText(string.IsNullOrEmpty(result.Denied) ? "제작을 못 했습니다." : result.Denied, TextType.Warning);
				return;
			}

			// 재료는 들었는데 주사위를 진 것과, 재료가 없어 못 한 것은 사람에게 전혀 다른 일이다.
			uiManager.PopText(result.Succeeded ? "제작 성공 !" : "제작 실패 !",
				result.Succeeded ? TextType.Heal : TextType.Warning);
		}

		private IEnumerator Loop()
		{
			UpdateRecipeGrid();
			recipeGrid.SelectSlot(0);
			recipeGrid.CurSlot.OnSelect(null);

			WaitForSeconds wait = new(TimeManager.TICK);
			while (true)
			{
				// Debug.Log($"{nameof(UICraft)} {nameof(Loop)}");
				ShowWorldCraftResult();
				UpdateUI();
				yield return wait;
			}
		}

		public override void Init()
		{
			foreach (UIItemSlot craftTableSlot in craftTableSlots)
				craftTableSlot.Init();
			foreach (UIItemSlot resultSlot in resultSlots)
				resultSlot.Init();
			recipeGrid.Init();
		}

		public override void UpdateUI()
		{
			foreach (UIItemSlot craftTableSlot in craftTableSlots)
				craftTableSlot.UpdateUI();
			foreach (UIItemSlot resultSlot in resultSlots)
				resultSlot.UpdateUI();
			UpdateRecipeGrid();
			UpdateTooltip();
		}

		private void UpdateRecipeGrid()
		{
			// 현재 가지고 있는 레시피만 보여주도록
			List<ItemData> availableRecipes = new();

			// 모든 아이템 데이터 중에서 해당 아이템 타입과 레시피 타입이 일치하는 아이템 데이터만 필터링
			ForEach<ItemData>(itemData =>
			{
				if (itemData.Unlocked)
					if ((itemData.Type == itemType) && (itemData.Recipes[0].Type == recipeType))
						availableRecipes.Add(itemData);
			});

			recipeGrid.SetData(availableRecipes);
			recipeGrid.UpdateUI();
		}

		/// <summary>세계의 제작표에서 그 줄을 찾는다 — 줄의 번호 = 결과 아이템 번호다.</summary>
		private static CraftRecipeEntry FindWorldRecipe(int itemId)
		{
			System.Collections.Generic.IReadOnlyList<CraftRecipeEntry> book = WorldCraftBridge.Channel.Recipes;
			for (int i = 0; book != null && i < book.Count; i++)
			{
				if (book[i] != null && book[i].id == itemId)
					return book[i];
			}

			return null;
		}

		private void UpdateTooltip()
		{
			if (percentageText == null || priceText == null)
				return;

			if (recipeGrid.CurSlotIndex < 0 || recipeGrid.CurSlotIndex >= recipeGrid.Data.Count)
			{
				percentageText.text = "_";
				priceText.text = "_";
				return;
			}

			ItemData itemData = recipeGrid.Data[recipeGrid.CurSlotIndex];
			Recipe recipe = itemData.Recipes[0];

			// ★ 여기는 <b>보여 주기만</b> 하는 자리다 (실측 2026-08-10): 한때 이 자리에 「만들겠다」
			//   요청이 들어가 있었다 — 툴팁이 갱신될 때마다 제작이 나갔다는 뜻이다(마우스만 옮겨도).
			//   요청은 사람이 버튼을 눌렀을 때(TryCraft)만 나간다.
			//
			// 세계에 붙어 있으면 <b>세계가 아는 재료</b>를 보여 준다 — 웹 창과 같은 글이어야 하므로
			// 계산은 판정 층(CraftAffordability)이 하고, 답은 골든 표가 묶는다.
			if (WorldCraftBridge.IsActive)
			{
				CraftRecipeEntry world = FindWorldRecipe(itemData.ID);
				if (world != null)
				{
					percentageText.text = CraftAffordability.NeedsText(
						world,
						id => SOManagerBridge.ItemInventory.CountByID(id),
						id => SOHelper.Get<ItemData>(id)?.Name);

					priceText.text = CraftAffordability.CanCraft(
						world, id => SOManagerBridge.ItemInventory.CountByID(id))
						? "만들 수 있다"
						: "재료가 모자란다";

					return;
				}
			}

			percentageText.text = $"{recipe.Percentage}%";
			priceText.text = $"{recipe.PriceNyang}냥";

			if (recipeType < RecipeType.Distillation)
			{
				for (int i = 0; i < craftTableSlots.Length; i++)
				{
					if (recipe.Items.Count > i)
					{
						ItemInfo ingredientInfo = recipe.Items[i];
						craftTableSlots[i].SetSlot(ingredientInfo.ItemData, ingredientInfo.Amount);

						// 인벤토리에 있는 해당 아이템의 양
						int amount = soManager.ItemInventory.GetItemAmount(ingredientInfo.ItemData.ID);
						craftTableAmounts[i].text = $"{(amount >= ingredientInfo.Amount ? "<color=white>" : "<color=red>")}{amount}</color>";
						craftTableAmounts[i].text += $"/{ingredientInfo.Amount}";
					}
					else
					{
						craftTableSlots[i].SetSlot(null);
					}
				}

				foreach (UIItemSlot resultSlot in resultSlots)
					resultSlot.SetSlot(null);
				resultSlots[0].SetSlot(itemData);
				resultAmounts[0].text = $"<color=white>{recipe.Amount}</color>";
			}
			else
			{
				foreach (UIItemSlot craftTableSlot in craftTableSlots)
					craftTableSlot.SetSlot(null);
				craftTableSlots[0].SetSlot(itemData);

				// 인벤토리에 있는 해당 아이템의 양
				int inventoryAmount = soManager.ItemInventory.GetItemAmount(itemData.ID);
				craftTableAmounts[0].text = $"{(inventoryAmount >= recipe.Amount ? "<color=white>" : "<color=red>")}{inventoryAmount}</color>";
				craftTableAmounts[0].text += $"/{recipe.Amount}";

				for (int i = 0; i < resultSlots.Length; i++)
				{
					if (recipe.Items.Count > i)
					{
						ItemInfo resultInfo = recipe.Items[i];
						resultSlots[i].SetSlot(resultInfo.ItemData, resultInfo.Amount);
						resultAmounts[i].text = $"<color=white>{resultInfo.Amount}</color>";
					}
					else
					{
						resultSlots[i].SetSlot(null);
					}
				}
			}
		}

		public void TryCraft()
		{
			Debug.Log(nameof(TryCraft));

			// Check Recipe
			if (recipeGrid.CurSlot == null)
			{
				uiManager.PopText("레시피를 선택해주세요.", TextType.Warning);
				return;
			}

			ItemData itemData = recipeGrid.Data[recipeGrid.CurSlotIndex];
			Recipe recipe = itemData.Recipes[0];

			// ★ 세계에 붙어 있으면 <b>세계가 판정한다</b> (TASK-WM-217): 재료 확인도, 성공 주사위도,
			//   지급도. 여기서 게임이 굴리면 창을 고친 사람은 언제나 성공하고, 게임 창과 웹 창이
			//   같은 재료로 서로 다른 결과를 본다(같은 세계가 아니게 된다).
			//   제작 줄의 번호 = 결과 아이템 번호다.
			if (WorldCraftBridge.IsActive)
			{
				WorldCraftBridge.Channel.Request(itemData.ID);
				return;
			}

			// Has Ingredients
			if (recipeType < RecipeType.Distillation)
			{
				foreach (ItemInfo ingredientInfo in recipe.Items)
				{
					int inventoryAmount = soManager.ItemInventory.GetItemAmount(ingredientInfo.ItemData.ID);
					if (inventoryAmount < ingredientInfo.Amount)
					{
						uiManager.PopText($"제작에 필요한 재료가 부족합니다. ({ingredientInfo.ItemData.Name})", TextType.Warning);
						return;
					}
				}

				// Check Nyang
				int recipePrice = recipe.PriceNyang;
				if (recipePrice > dataManager.GameStat[GameStatType.NYANG])
				{
					int diff = recipePrice - dataManager.GameStat[GameStatType.NYANG];
					uiManager.PopText($"제작에 필요한 냥이 부족합니다. ({diff}냥)", TextType.Warning);
				}

				// Craft
				// 1. Remove Ingredients
				// 쓰는 규칙은 판정 층에 있다 (TASK-WM-215) — 흩어진 칸을 모아 쓰고, 모자라면 남은 수를 알려준다.
				foreach (ItemInfo ingredientInfo in recipe.Items)
				{
					soManager.ItemInventory.Consume(ingredientInfo.ItemData.ID, ingredientInfo.Amount);
				}

				dataManager.GameStat[GameStatType.NYANG] -= recipePrice;
				uiManager.PopText($"- {recipePrice}", TextType.Warning);

				// 2. Craft
				if (Random.Range(0, 100) > recipe.Percentage)
				{
					// Fail
					Reward.GetReward(recipe.FailureRewards, soManager.ItemInventory, dataManager.GameStat);
					uiManager.PopText("제작 실패 !", TextType.Warning);
				}
				else
				{
					// Success
					Reward.GetReward(recipe.SuccessRewards, soManager.ItemInventory, dataManager.GameStat);
					uiManager.PopText("제작 성공 !", TextType.Heal);
					soManager.ItemInventory.Add(itemData, 1);
				}

				UpdateUI();
			}
			else
			{
				int inventoryAmount = soManager.ItemInventory.GetItemAmount(itemData.ID);
				if (inventoryAmount < recipe.Amount)
				{
					uiManager.PopText($"제작에 필요한 재료가 부족합니다. ({itemData.Name})", TextType.Warning);
					return;
				}

				// Check Nyang
				int recipePrice = recipe.PriceNyang;
				if (recipePrice > dataManager.GameStat[GameStatType.NYANG])
				{
					int diff = recipePrice - dataManager.GameStat[GameStatType.NYANG];
					uiManager.PopText($"제작에 필요한 냥이 부족합니다. ({diff}냥)", TextType.Warning);
				}

				// Craft
				// 1. Remove Ingredients
				// 쓰는 규칙은 판정 층에 있다 (TASK-WM-215).
				soManager.ItemInventory.Consume(itemData.ID, recipe.Amount);

				dataManager.GameStat[GameStatType.NYANG] -= recipePrice;
				uiManager.PopText($"- {recipePrice}", TextType.Warning);

				// 2. Craft
				if (Random.Range(0, 100) > recipe.Percentage)
				{
					// Fail
					Reward.GetReward(recipe.FailureRewards, soManager.ItemInventory, dataManager.GameStat);
					uiManager.PopText("제작 실패 !", TextType.Warning);
				}
				else
				{
					// Success
					Reward.GetReward(recipe.SuccessRewards, soManager.ItemInventory, dataManager.GameStat);
					uiManager.PopText("제작 성공 !", TextType.Heal);

					foreach (ItemInfo resultInfo in recipe.Items)
						soManager.ItemInventory.Add(resultInfo.ItemData, resultInfo.Amount);
				}

				UpdateUI();
			}
		}
	}
}