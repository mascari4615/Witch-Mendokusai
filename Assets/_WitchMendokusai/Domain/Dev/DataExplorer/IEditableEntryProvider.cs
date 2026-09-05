// TASK-WM-038 단계 B — 편집 hook 인터페이스 (Add/Copy/Remove + OnEntryActivated).
namespace WitchMendokusai
{
	/// <summary>
	/// 편집 가능한 데이터 탐색 provider — Add/Copy/Remove + entry activate hook.
	/// 에디터 (DataSOWindow 등) 가 사용. 런타임 Codex 는 base IEntryProvider 만.
	/// </summary>
	public interface IEditableEntryProvider : IEntryProvider
	{
		bool CanAdd { get; }
		bool CanCopy { get; }
		bool CanRemove { get; }

		void Add();
		void Copy(EntryDescriptor entry);
		void Remove(EntryDescriptor entry);

		/// <summary>entry 활성 시 외부 부수 동작 (예: Selection.activeObject = entry.Source).</summary>
		void OnEntryActivated(EntryDescriptor entry);
	}
}
