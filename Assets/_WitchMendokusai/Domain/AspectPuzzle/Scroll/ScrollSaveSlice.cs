using System.Collections.Generic;
using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 봉인된 두루마리로 연 것. 항목 번호마다 열린 모습 수. 조각이 없는 옛 세이브는 아무것도 모르는 데서 시작.
	/// 만드는 법은 여기에도 남고, 제작대가 읽는 DataManager.IsRecipeUnlocked 에도 씀 (그쪽이 정본)
	/// </summary>
	public sealed class ScrollSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "scroll";

		[JsonIgnore]
		public string Key => KEY;

		public ScrollKnowledge Knowledge { get; set; } = new();

		public void Reset() => Knowledge = new();

		public string Capture() => JsonConvert.SerializeObject(this);

		public void Restore(string json)
		{
			ScrollSaveSlice loaded = JsonConvert.DeserializeObject<ScrollSaveSlice>(json);
			Knowledge = loaded.Knowledge ?? new();
			Knowledge.OpenedForms ??= new Dictionary<int, int>();
		}

		public void RestoreLegacy(GameData saveData) => Knowledge = new();
	}
}
