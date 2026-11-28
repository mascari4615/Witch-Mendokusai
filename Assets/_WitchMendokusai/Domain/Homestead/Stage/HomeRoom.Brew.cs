using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 작업방 솥. 솥 화면 (BrewPage) 의 부탁을 받아 솥 지도 (CauldronMapController) 를 빌려 엶
	// - 재료 버튼은 창고 물건. 넣을 때는 수만 세고 (brewUsed), 다 만든 때 Sim 이 한 번에 냄
	// - 지도 끌기는 막음 (재료 없이 움직이면 셈이 안 맞음)
	// - 닫거나 다시 저으면 아무것도 잃지 않음. 닫히면 솥 화면으로 돌아감
	public sealed partial class HomeRoom
	{
		private readonly List<string> brewUsed = new();
		private CauldronMapElement brewMap;
		private string brewId = string.Empty;

		private string BrewRole()
		{
			if (Sim.HasPotion == false)
				return "물약, 선반은 비어 있음";
			return $"물약, 선반에 {Sim.Catalog.Get<GoodsDef>(Sim.State.Potion).Name}";
		}

		private void BeginBrew(string id)
		{
			if (brewMap != null)
				return;
			// 솥 지도 갈래 (AlchemyMapFeature) 가 씬에 없으면 조립이 잘못된 것
			CauldronMapController controller = CauldronMapController.Instance;
			if (controller == null)
				throw new MissingReferenceException($"{nameof(CauldronMapController)} 가 씬에 없음. FeatureManifest 의 AlchemyMapFeature 확인");
			// 세계 솥 (공유 채널) 은 집 창고를 모름. 이어진 동안은 집 솥을 안 엶
			if (SharedBrewChannelBridge.IsActive)
			{
				manager.BrewNews = "세계 솥에 이어져 있어 집 솥을 쓸 수 없습니다";
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				return;
			}
			BrewDef brew = Sim.Catalog.Get<BrewDef>(id);
			brewId = id;
			brewUsed.Clear();
			manager.Panels.ClosePanel();
			brewMap = controller.Borrow(OnBrewClosed);
			string shownName = Sim.IsBrewKnown(id) ? brew.Name : "모르는 물약";
			brewMap.Setup(HomesteadBrewMap.Recipe(brew, shownName), HomesteadBrewMap.Hazards(Sim.Catalog), BrewPalette(), BrewOutcomeRules.Default, BrewSpell(shownName));
			brewMap.AllowDrag = false;
			brewMap.IngredientGate = CanAddBrewIngredient;
			brewMap.IngredientAdded = OnBrewIngredientAdded;
			brewMap.SessionRestarted = OnBrewEmptied;
			brewMap.BrewCompleted = OnBrewCompleted;
		}

		private static string BrewSpell(string shownName) =>
			$"{shownName}: 재료를 넣어 검은 점을 초록 원 안으로.\n가운데에 깨끗이 닿으면 하나 더, 붉은 얼룩을 지나면 덤을 놓칩니다.\n못 닿아도 흐린 물약이 나옵니다.";

		// 버튼 글은 이름과 남은 수 (창고 수에서 이번 솥에 넣은 수를 뺌)
		private List<CauldronMapElement.Ingredient> BrewPalette()
		{
			List<CauldronMapElement.Ingredient> palette = new();
			foreach (BrewIngredientDef ingredient in Sim.Catalog.BrewIngredients)
			{
				int left = Sim.Count(ingredient.Id) - brewUsed.FindAll(goodsId => goodsId == ingredient.Id).Count;
				palette.Add(HomesteadBrewMap.Ingredient(ingredient, $"{Sim.Catalog.Get<GoodsDef>(ingredient.Id).Name} {left}"));
			}
			return palette;
		}

		// 모자라면 안 넣고 실패음. 버튼이 죽은 것처럼 보이지 않게
		private bool CanAddBrewIngredient(int index)
		{
			bool ok = Sim.CanAddBrewIngredient(Sim.Catalog.BrewIngredients[index].Id, brewUsed).Ok;
			if (ok == false)
				UiSfx.Play(UiSfxEvent.PurchaseFail);
			return ok;
		}

		private void OnBrewIngredientAdded(int index)
		{
			brewUsed.Add(Sim.Catalog.BrewIngredients[index].Id);
			brewMap.SetPalette(BrewPalette());
		}

		private void OnBrewEmptied()
		{
			brewUsed.Clear();
			if (brewMap != null)
				brewMap.SetPalette(BrewPalette());
		}

		// 지도가 채점한 대로. 칸에 닿았으면 그 물약, 아니면 흐린 물약. 재료를 안 넣었으면 실패음만 (지도는 빈 솥으로 남음)
		private void OnBrewCompleted(BrewOutcome outcome)
		{
			if (brewUsed.Count == 0)
			{
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				return;
			}
			BrewDef brew = Sim.Catalog.Get<BrewDef>(brewId);
			bool wasKnown = Sim.IsBrewKnown(brewId);
			bool masterwork = outcome.Grade == BrewGrade.Masterwork;
			HomesteadResult result = outcome.Reached ? Sim.BrewReached(brewId, brewUsed, masterwork) : Sim.BrewMissed(brewUsed);
			manager.BrewNews = BrewNews(brew, outcome.Reached, masterwork, wasKnown, result);
			if (result.Ok)
				manager.Save();
			UiSfx.Play(result.Ok == false ? UiSfxEvent.PurchaseFail : masterwork ? UiSfxEvent.LevelUp : UiSfxEvent.Purchase);
			CauldronMapController.Instance.Close();
		}

		private string BrewNews(BrewDef brew, bool reached, bool masterwork, bool wasKnown, HomesteadResult result)
		{
			if (result.Ok == false)
				return "재료가 모자라 못 만들었습니다";
			if (reached == false)
			{
				string kept = Sim.BrewMissKeeps(brewUsed.Count) ? $", {Sim.Catalog.Get<GoodsDef>(brewUsed[brewUsed.Count - 1]).Name} 하나는 돌려받음" : string.Empty;
				return $"흐린 물약 하나{kept}";
			}
			int amount = Sim.BrewAmount(masterwork);
			string note = wasKnown ? string.Empty : ". 처방 노트에 적었습니다";
			return $"{brew.Name} {amount}개{note}";
		}

		private void OnBrewClosed()
		{
			bool wasOpen = brewMap != null;
			brewMap = null;
			brewUsed.Clear();
			brewId = string.Empty;
			if (wasOpen && manager != null && manager.Panels != null)
				manager.Panels.Show(HomesteadPanelType.Brew);
		}

		// 방을 떠나면 빌린 지도도 닫음 (솥 화면은 다시 열지 않음)
		private void StopBrew()
		{
			if (brewMap == null)
				return;
			brewMap = null;
			brewUsed.Clear();
			brewId = string.Empty;
			if (CauldronMapController.Instance != null)
				CauldronMapController.Instance.Close();
		}
	}
}
