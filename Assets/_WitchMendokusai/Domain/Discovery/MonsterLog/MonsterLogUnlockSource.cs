using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>몬스터 갈래의 해금 출처. 잡았거나 쓰러진 적이 있는 종만 열림</summary>
	public sealed class MonsterLogUnlockSource : IDiscoveryUnlockSource
	{
		private readonly MonsterLogManager manager;

		public MonsterLogUnlockSource(MonsterLogManager manager)
		{
			this.manager = manager;
		}

		public string CatalogId => MonsterDiscoveryCategory.ID;

		public bool IsUnlocked(string entryId) => manager.Book != null && manager.Book.IsMet(MonsterDiscoveryCategory.MonsterIdOf(entryId));
	}
}
