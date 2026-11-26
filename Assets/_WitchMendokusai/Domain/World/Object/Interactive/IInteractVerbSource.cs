namespace WitchMendokusai
{
	// 손가락 상호작용 버튼에 띄울 짧은 동사 (갈기, 낚시, 깨기). 빈 글자면 버튼은 기본 그림
	// 표시 글자 (IInteractPromptSource.PromptRole) 와 따로 둠. 그쪽은 "1 / 3" 같은 값도 들어감
	public interface IInteractVerbSource
	{
		string ActionVerb { get; }
	}
}
