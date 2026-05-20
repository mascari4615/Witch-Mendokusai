namespace WitchMendokusai
{
	// UGC sandbox 이음매 — JSON 으로 들어온 SeedSaveData (POCO, 5 noise 필드만) 가 TerrainParameters (DataSO) 로 흐르는 *유일한 통로*.
	// TerrainParameters 의 amplitude / seed / biomes / heightmap 은 의도적으로 무시 — 사용자 UGC 가 schema 밖 필드를 *건드릴 수 없음* 을 코드 위치로 못 박는다.
	public static class SeedSaveDataExtensions
	{
		public static void ApplyTo(this SeedSaveData seedData, TerrainParameters target)
		{
			target.SetOctaves(seedData.octaves);
			target.SetFrequency(seedData.frequency);
			target.SetPersistence(seedData.persistence);
			target.SetLacunarity(seedData.lacunarity);
			target.SetBiomeFrequency(seedData.biomeFrequency);
		}
	}
}
