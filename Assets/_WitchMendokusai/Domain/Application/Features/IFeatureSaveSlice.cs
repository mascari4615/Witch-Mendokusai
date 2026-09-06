namespace WitchMendokusai
{
	/// <summary>
	/// 갈래 하나가 세이브에 남기는 조각.
	///
	/// 왜: 공용 저장 (GameData, SaveManager) 이 갈래 필드를 알면 갈래를 못 뗌
	/// 뒤집기. 공용은 조각 목록을 돌며 글자 (json) 만 옮기고, 무엇을 남길지는 갈래가 앎
	/// 자리는 GameData.featureSaves 의 Key 한 칸. 목록은 <see cref="FeatureManifest"/>
	/// </summary>
	public interface IFeatureSaveSlice
	{
		/// <summary>featureSaves 의 열쇠. 갈래마다 하나, 바꾸면 옛 세이브의 조각을 못 찾음</summary>
		string Key { get; }

		/// <summary>새 게임. 조각을 처음 상태로</summary>
		void Reset();

		/// <summary>세이브에 넣을 글자</summary>
		string Capture();

		/// <summary><see cref="Capture"/> 가 낸 글자를 되돌림. 조각 전체를 덮음</summary>
		void Restore(string json);

		/// <summary>
		/// 조각이 생기기 전 세이브. 갈래 필드가 GameData 에 직접 있던 시절 (2026-09-06 전)
		/// 옛 필드를 읽어 채움. 옛 필드가 없는 갈래는 no-op
		/// </summary>
		void RestoreLegacy(GameData saveData);
	}
}
