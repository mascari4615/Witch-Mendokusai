using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 식물 갈래의 해금 출처. 조건은 표본으로 남았나 하나 (DataManager.SpecimenCollected).
	///
	/// 조건을 아는 쪽이 답하는 자리. 도감은 받아 표시만.
	/// DataManager 는 매번 찾음 (부팅 순서와 씬 재진입 탓. 참조를 들고 있으면 낡음).
	/// </summary>
	public class PlantSpecimenUnlockSource : IDiscoveryUnlockSource
	{
		public string CatalogId => PlantDiscoveryCategory.CATALOG_ID;

		public bool IsUnlocked(string entryId)
		{
			if (PlantDiscoveryCategory.TryParseEntryId(entryId, out int plantDataId) == false)
			{
				return false;
			}

			if (DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
			{
				return false;
			}

			return dataManager.SpecimenCollected.TryGetValue(plantDataId, out bool collected) && collected;
		}
	}
}
