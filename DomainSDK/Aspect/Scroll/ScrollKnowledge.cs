using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>
	/// 두루마리로 무엇을 얼마나 열었는지. 항목 번호마다 열린 모습 수 (0 이면 모름, 1 이면 배움).
	/// 세이브에는 이 사전 그대로 숫자로 실림 (이름으로 두면 개명 때 세이브가 안 열림)
	/// </summary>
	public sealed class ScrollKnowledge
	{
		public Dictionary<int, int> OpenedForms { get; set; } = new Dictionary<int, int>();

		public int Opened(int entryId) => OpenedForms.TryGetValue(entryId, out int opened) ? opened : 0;

		public bool IsLearned(int entryId) => Opened(entryId) > 0;

		public bool IsComplete(ScrollEntry entry) => Opened(entry.Id) >= entry.Forms;
	}
}
