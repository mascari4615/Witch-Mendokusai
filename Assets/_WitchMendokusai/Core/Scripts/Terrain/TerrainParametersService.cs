using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 런타임이 참조하는 활성 TerrainParameters 진입점.
	/// 에디터의 Terrain Editor에서 "Apply to Active"로 값을 갱신한다.
	/// </summary>
	public static class TerrainParametersService
	{
		public const string ACTIVE_RESOURCE_PATH = "Terrain/TerrainParameters_Active";

		private static TerrainParameters cachedActive;

		public static TerrainParameters Active
		{
			get
			{
				if (cachedActive == null)
					cachedActive = Resources.Load<TerrainParameters>(ACTIVE_RESOURCE_PATH);
				return cachedActive;
			}
		}

		public static void ClearCache() => cachedActive = null;
	}
}
