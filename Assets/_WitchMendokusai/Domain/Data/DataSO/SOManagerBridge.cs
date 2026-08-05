using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class SOManagerBridge
	{
		private static SOManager _instance;
		public static void Register(SOManager soManager) => _instance = soManager;
		// 부트(Core 매니저 Awake 의 Register) 전엔 _instance == null — DataSOs 접근 NRE 방지용 가드.
		// EditMode·씬 드롭 직후 등 런타임 데이터가 아직 로드 안 된 시점에서 호출자가 안전하게 우회하게 한다.
		public static bool HasInstance => _instance != null;
		public static Inventory ItemInventory => _instance.ItemInventory;
		public static CardBuffer SelectedCardBuffer => _instance.SelectedCardBuffer;
		public static QuestSOBuffer VQuests => _instance.VQuests;
		public static Dictionary<Type, Dictionary<int, DataSO>> DataSOs => _instance.DataSOs;
	}
}
