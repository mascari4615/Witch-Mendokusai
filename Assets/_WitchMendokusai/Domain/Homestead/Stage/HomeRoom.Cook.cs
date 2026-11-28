using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 부엌. 요리 화면 (CookPage) 의 굽기 부탁을 받아 바늘 하나를 돌림
	// - 입력은 낚시 화면 (UIFishingPanel) 을 그대로 씀: 누르기 하나, 그만두기, 닫으면 그만
	// - 막대는 낚시 HUD 막대 (알맞은 칸 포함) 를 그대로 씀
	// - 그만두면 재료는 그대로 (Sim.Cook 은 다 구운 때만)
	public sealed partial class HomeRoom
	{
		[Header("Cook")]
		[Tooltip("다 된 뒤 결과 글을 보이는 초. 지나면 부엌 화면으로 돌아감")]
		[SerializeField] private float cookResultSeconds = 1.2f;

		private CookTiming cookTiming;
		private string cookId = string.Empty;
		private float cookResultTimer;
		private bool cookFinished;
		private HomesteadResult cookResult = HomesteadResult.Success;

		private string CookRole()
		{
			if (Sim.HasMeal == false)
				return "요리, 식탁은 비어 있음";
			return $"요리, 식탁에 {Sim.Catalog.Get<GoodsDef>(Sim.State.Meal).Name}";
		}

		private void BeginCook(string id)
		{
			if (cookTiming != null || Sim.CanCook(id).Ok == false)
				return;
			cookId = id;
			cookTiming = Sim.StartCookTiming();
			cookFinished = false;
			cookResultTimer = 0f;
			cookResult = HomesteadResult.Success;
			manager.Hud.ShowDuel(true);
			manager.Hud.SetCook(cookTiming, Sim.Catalog.Get<CookDef>(id).Name, cookFinished, cookResult);
			// 누르는 순간만 셈. 떼는 것은 굽기에 뜻이 없어 빈 동작
			manager.Panels.Fishing.Bind(PressCook, () => { }, EndCook, StopCook);
			manager.Panels.Show(HomesteadPanelType.Fishing);
			manager.Panels.Fishing.ShowButton("누르기");
		}

		private void PressCook()
		{
			if (cookTiming == null)
				return;
			cookTiming.Press();
		}

		private void Update()
		{
			if (cookTiming == null)
				return;
			cookTiming.Step(Time.deltaTime);
			if (cookTiming.IsOver && cookFinished == false)
			{
				cookFinished = true;
				// 굽는 사이 재료가 줄 수 있음 (세이브를 다시 읽음 등). 그때는 아무것도 안 나오고 실패 글과 소리
				cookResult = Sim.Cook(cookId, cookTiming.IsWellDone);
				if (cookResult.Ok)
					manager.Save();
				UiSfx.Play(cookResult.Ok == false ? UiSfxEvent.PurchaseFail : cookTiming.IsWellDone ? UiSfxEvent.LevelUp : UiSfxEvent.Purchase);
			}
			manager.Hud.SetCook(cookTiming, Sim.Catalog.Get<CookDef>(cookId).Name, cookFinished, cookResult);
			if (cookFinished == false)
				return;
			cookResultTimer += Time.deltaTime;
			if (cookResultTimer >= cookResultSeconds)
				FinishCook();
		}

		// 다 되면 부엌 화면으로 돌아감. 같은 요리를 이어 하기 좋게
		private void FinishCook()
		{
			StopCook();
			manager.Panels.Show(HomesteadPanelType.Cook);
		}

		// 화면을 닫으면 EndCook 이 뒤따름
		private void StopCook()
		{
			if (cookTiming == null)
				return;
			if (manager != null && manager.Panels != null && manager.Panels.CurPanelType == HomesteadPanelType.Fishing)
				manager.Panels.ClosePanel();
			EndCook();
		}

		private void EndCook()
		{
			if (cookTiming == null)
				return;
			cookTiming = null;
			cookId = string.Empty;
			if (manager != null && manager.Hud != null)
				manager.Hud.ShowDuel(false);
		}
	}
}
