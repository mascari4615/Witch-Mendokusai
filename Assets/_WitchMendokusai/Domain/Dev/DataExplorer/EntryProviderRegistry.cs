using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// IEntryProvider list. 도메인별로 instance 보유 (Discovery 도메인 = 별도, DataSOWindow 도메인 = 별도).
	/// (구) DiscoveryCategoryRegistry 의 일반화 — Singleton 폐기, 도메인 controller 가 instance 보유.
	/// </summary>
	public class EntryProviderRegistry
	{
		private readonly List<IEntryProvider> providers = new();

		public IReadOnlyList<IEntryProvider> Providers => providers;

		public void Register(IEntryProvider provider)
		{
			providers.Add(provider);
		}

		public IEntryProvider FindById(string id)
		{
			for (int i = 0; i < providers.Count; i++)
			{
				if (providers[i].Id == id)
					return providers[i];
			}
			return null;
		}

		public void Clear() => providers.Clear();
	}
}
