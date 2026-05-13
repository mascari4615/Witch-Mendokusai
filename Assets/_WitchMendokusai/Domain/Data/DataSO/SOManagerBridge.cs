using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class SOManagerBridge
	{
		private static SOManager _instance;
		public static void Register(SOManager soManager) => _instance = soManager;
		public static Inventory ItemInventory => _instance.ItemInventory;
		public static CardBuffer SelectedCardBuffer => _instance.SelectedCardBuffer;
		public static QuestSOBuffer VQuests => _instance.VQuests;
		public static Dictionary<Type, Dictionary<int, DataSO>> DataSOs => _instance.DataSOs;
	}
}
