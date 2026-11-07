namespace WitchMendokusai
{
	// 패널 안에서 취소와 Tab의 뜻을 바꾸는 패널
	public interface IUIPanelInputOwner
	{
		bool OwnsTabInput { get; }

		// true면 입력 소비, false면 기본 닫기
		bool HandleCancelInput();
	}
}
