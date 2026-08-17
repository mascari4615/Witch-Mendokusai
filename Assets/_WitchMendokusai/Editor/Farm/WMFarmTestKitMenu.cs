using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 밭을 눈으로 확인하려면 손에 <b>괭이와 씨앗</b>이 있어야 한다 — 지금은 그것을 얻을 길이
	/// (상점·제작·보상) 아직 없다. 그래서 검증용으로 바로 쥐어 준다.
	///
	/// ★ 왜 에디터 메뉴인가: 게임 안에 「치트로 아이템 받기」를 심으면 그게 제품에 남는다.
	///   이건 확인이 끝나면 지워도 되는 <b>도구</b>다.
	/// ⚠ Play 중에만 동작한다 — 인벤토리는 게임이 떠 있어야 존재한다.
	/// </summary>
	public static class WMFarmTestKitMenu
	{
		private const string MENU_PATH = "WM/Farm/시험 괭이·씨앗 손에 쥐어주기";
		private const int TEST_HOE_ID = 20000900;
		private const int TEST_SEED_ID = 30000900;
		private const int SEED_COUNT = 10;

		[MenuItem(MENU_PATH)]
		public static void GiveTestKit()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning("[WM/Farm] Play 중에만 쥐어 줄 수 있다 — 게임이 떠 있어야 가방이 있다.");
				return;
			}

			SOManager soManager = SOManager.Instance;
			if (soManager == null || soManager.ItemInventory == null)
			{
				Debug.LogError("[WM/Farm] 가방을 못 찾았다 (SOManager/ItemInventory 미배선).");
				return;
			}

			ItemData hoe = SOHelper.Get<ItemData>(TEST_HOE_ID);
			ItemData seed = SOHelper.Get<ItemData>(TEST_SEED_ID);

			if (hoe == null || seed == null)
			{
				Debug.LogError($"[WM/Farm] 시험 데이터를 못 찾았다 — 괭이={hoe} 씨앗={seed}. Addressables 등록을 확인할 것.");
				return;
			}

			soManager.ItemInventory.Add(hoe, 1);
			soManager.ItemInventory.Add(seed, SEED_COUNT);

			if (soManager.Hotbar != null)
			{
				soManager.Hotbar.Add(hoe, 1);
				soManager.Hotbar.Add(seed, SEED_COUNT);
			}

			Debug.Log($"[WM/Farm] 괭이 1 · 씨앗 {SEED_COUNT} 을 쥐어 줬다. 핫바에서 괭이를 골라 땅을 우클릭하면 갈린다.");
		}
	}
}
