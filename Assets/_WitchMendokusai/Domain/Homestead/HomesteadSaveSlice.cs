using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	/// <summary>생활 갈래가 세이브에 남기는 것. 규칙 쪽 상태를 통째로</summary>
	public sealed class HomesteadSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "homestead";

		public string Key => KEY;

		public HomesteadState State { get; private set; } = new();

		public void Reset() => State = new();

		public string Capture() => JsonConvert.SerializeObject(State);

		public void Restore(string json) => State = JsonConvert.DeserializeObject<HomesteadState>(json) ?? new();

		// 조각이 생기기 전 세이브에는 생활 자료가 없음
		public void RestoreLegacy(GameData saveData) => Reset();
	}
}
