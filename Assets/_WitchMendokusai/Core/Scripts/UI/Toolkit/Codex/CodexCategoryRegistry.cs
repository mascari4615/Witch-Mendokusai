using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 카테고리 list. 사이드바는 등록 순서로 그려짐.
	/// 순수 C# 싱글톤 (MonoBehaviour 아님) — DevModeRegistry 와 같은 모양.
	/// </summary>
	public class CodexCategoryRegistry
	{
		private static CodexCategoryRegistry instance;
		public static CodexCategoryRegistry Instance => instance ??= new CodexCategoryRegistry();

		private readonly List<ICodexCategory> categories = new();

		public IReadOnlyList<ICodexCategory> Categories => categories;

		public void Register(ICodexCategory category)
		{
			categories.Add(category);
		}

		public ICodexCategory FindById(string id)
		{
			for (int i = 0; i < categories.Count; i++)
			{
				if (categories[i].Id == id)
					return categories[i];
			}
			return null;
		}

		public void Clear() => categories.Clear();
	}
}
