namespace WitchMendokusai
{
	// 상호작용 표시 (InteractPromptView) 의 글자를 스스로 정하는 대상. 없으면 오브젝트 이름
	public interface IInteractPromptSource
	{
		string PromptName { get; }
		string PromptRole { get; }

		// 글자가 바뀔 때마다 달라지는 값
		int PromptStamp { get; }
	}
}
