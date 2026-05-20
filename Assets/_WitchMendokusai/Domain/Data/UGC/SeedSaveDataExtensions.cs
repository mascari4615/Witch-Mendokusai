namespace WitchMendokusai
{
	// SeedSaveData → TerrainParameters 유일한 통로. amplitude/seed/biomes/heightmap 는 schema 밖 — 의도적 무시로 sandbox 표면을 코드 위치로 못 박는다.
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
