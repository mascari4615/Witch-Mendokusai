using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 밭을 눈으로 보려면 씬에 두 가지가 있어야 한다 — 세계(<see cref="WorldActSite"/>)와
	/// 밭(<see cref="FarmGroundObject"/>). 손으로 붙이면 「World 를 안 물려서 아무 일도 안 남」이
	/// 첫 사고가 되므로(원장은 빈 세계를 조용히 통과시킨다) 그 배선을 한 번에 해 준다.
	///
	/// 메뉴 루트는 `WM/` 단일 (Editor 메뉴 룰).
	/// </summary>
	public static class WMFarmSceneMenu
	{
		private const string MENU_PATH = "WM/Farm/씬에 밭 세계 놓기";
		private const string SITE_NAME = "WorldActSite";
		private const string FARM_NAME = "FarmGround";

		[MenuItem(MENU_PATH)]
		public static void PlaceFarmWorld()
		{
			GameObject siteObject = new(SITE_NAME);
			Undo.RegisterCreatedObjectUndo(siteObject, "밭 세계 놓기");
			WorldActSite site = Undo.AddComponent<WorldActSite>(siteObject);

			GameObject farmObject = new(FARM_NAME);
			Undo.RegisterCreatedObjectUndo(farmObject, "밭 놓기");
			Undo.SetTransformParent(farmObject.transform, siteObject.transform, "밭 붙이기");
			Undo.AddComponent<FarmGroundObject>(farmObject);

			Selection.activeGameObject = siteObject;
			EditorGUIUtility.PingObject(siteObject);

			Debug.Log(
				$"[WM/Farm] 세계와 밭을 놓았다. 남은 손질 두 가지:\n" +
				$"  ① {SITE_NAME} 의 '창고' 에 플레이어 Inventory 를 물린다 (안 물리면 씨앗을 못 꺼내 심기가 거절된다).\n" +
				$"  ② 심을 씨앗(SeedItemData) 의 'Plant' 에 마도작물(WitchPlantSO) 을 물린다 (비어 있으면 옛 경로로 간다).\n" +
				$"그다음 Play → 복셀 땅을 우클릭: 굳은 흙이면 갈고, 갈린 자리면 심고, 개화했으면 거둔다.",
				site);
		}
	}
}
