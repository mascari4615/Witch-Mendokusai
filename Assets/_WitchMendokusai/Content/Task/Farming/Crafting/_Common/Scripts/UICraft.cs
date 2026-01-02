using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

		private IEnumerator Loop()
		{
			UpdateRecipeGrid();
			recipeGrid.SelectSlot(0);
			recipeGrid.CurSlot.OnSelect(null);

			WaitForSeconds wait = new(TimeManager.TICK);
			while (true)
			{
				// Debug.Log($"{nameof(UICraft)} {nameof(Loop)}");
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
			recipeGrid.SetFilterFunc(itemData =>
			{
				// 1. 현재 가지고 있는 레시피 중
				// 2. 필터 아이템 타입과 레시피 타입이 일치하는 아이템 데이터
				return itemData.Unlocked && (itemData.Type == itemType) && (itemData.Recipes[0].Type == recipeType);
			});

			recipeGrid.UpdateUI();
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
						int amount = SOManager.Instance.ItemInventory.GetItemAmount(ingredientInfo.ItemData.ID);
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
				int inventoryAmount = SOManager.Instance.ItemInventory.GetItemAmount(itemData.ID);
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
				UIManager.Instance.PopText("레시피를 선택해주세요.", TextType.Warning);
				return;
			}

			ItemData itemData = recipeGrid.Data[recipeGrid.CurSlotIndex];
			Recipe recipe = itemData.Recipes[0];

			// Has Ingredients
			if (recipeType < RecipeType.Distillation)
			{
				foreach (ItemInfo ingredientInfo in recipe.Items)
				{
					int inventoryAmount = SOManager.Instance.ItemInventory.GetItemAmount(ingredientInfo.ItemData.ID);
					if (inventoryAmount < ingredientInfo.Amount)
					{
						UIManager.Instance.PopText($"제작에 필요한 재료가 부족합니다. ({ingredientInfo.ItemData.Name})", TextType.Warning);
						return;
					}
				}

				// Check Nyang
				int recipePrice = recipe.PriceNyang;
				if (recipePrice > DataManager.Instance.GameStat[GameStatType.NYANG])
				{
					int diff = recipePrice - DataManager.Instance.GameStat[GameStatType.NYANG];
					UIManager.Instance.PopText($"제작에 필요한 냥이 부족합니다. ({diff}냥)", TextType.Warning);
				}

				// Craft
				// 1. Remove Ingredients
				foreach (ItemInfo ingredientInfo in recipe.Items)
				{
					int remain = ingredientInfo.Amount;

					while (remain > 0)
					{
						int slotIndex = SOManager.Instance.ItemInventory.FindItemIndex(ingredientInfo.ItemData.ID);

						Item item = SOManager.Instance.ItemInventory.GetItem(slotIndex);
						int slotAmount = item.Amount;

						if (slotAmount > remain)
						{
							SOManager.Instance.ItemInventory.SetItemAmount(slotIndex, slotAmount - remain);
							break;
						}
						else
						{
							SOManager.Instance.ItemInventory.Remove(slotIndex);
							remain -= slotAmount;
						}
					}
				}

				DataManager.Instance.GameStat[GameStatType.NYANG] -= recipePrice;
				UIManager.Instance.PopText($"- {recipePrice}", TextType.Warning);

				// 2. Craft
				if (Random.Range(0, 100) > recipe.Percentage)
				{
					// Fail
					Reward.GetReward(recipe.FailureRewards);
					UIManager.Instance.PopText("제작 실패 !", TextType.Warning);
				}
				else
				{
					// Success
					Reward.GetReward(recipe.SuccessRewards);
					UIManager.Instance.PopText("제작 성공 !", TextType.Heal);
					SOManager.Instance.ItemInventory.Add(itemData, 1);
				}

				UpdateUI();
			}
			else
			{
				int inventoryAmount = SOManager.Instance.ItemInventory.GetItemAmount(itemData.ID);
				if (inventoryAmount < recipe.Amount)
				{
					UIManager.Instance.PopText($"제작에 필요한 재료가 부족합니다. ({itemData.Name})", TextType.Warning);
					return;
				}

				// Check Nyang
				int recipePrice = recipe.PriceNyang;
				if (recipePrice > DataManager.Instance.GameStat[GameStatType.NYANG])
				{
					int diff = recipePrice - DataManager.Instance.GameStat[GameStatType.NYANG];
					UIManager.Instance.PopText($"제작에 필요한 냥이 부족합니다. ({diff}냥)", TextType.Warning);
				}

				// Craft
				// 1. Remove Ingredients
				int remain = recipe.Amount;

				while (remain > 0)
				{
					int slotIndex = SOManager.Instance.ItemInventory.FindItemIndex(itemData.ID);

					Item item = SOManager.Instance.ItemInventory.GetItem(slotIndex);
					int slotAmount = item.Amount;

					if (slotAmount > remain)
					{
						SOManager.Instance.ItemInventory.SetItemAmount(slotIndex, slotAmount - remain);
						break;
					}
					else
					{
						SOManager.Instance.ItemInventory.Remove(slotIndex);
						remain -= slotAmount;
					}
				}

				DataManager.Instance.GameStat[GameStatType.NYANG] -= recipePrice;
				UIManager.Instance.PopText($"- {recipePrice}", TextType.Warning);

				// 2. Craft
				if (Random.Range(0, 100) > recipe.Percentage)
				{
					// Fail
					Reward.GetReward(recipe.FailureRewards);
					UIManager.Instance.PopText("제작 실패 !", TextType.Warning);
				}
				else
				{
					// Success
					Reward.GetReward(recipe.SuccessRewards);
					UIManager.Instance.PopText("제작 성공 !", TextType.Heal);

					foreach (ItemInfo resultInfo in recipe.Items)
						SOManager.Instance.ItemInventory.Add(resultInfo.ItemData, resultInfo.Amount);
				}

				UpdateUI();
			}
		}
	}
}