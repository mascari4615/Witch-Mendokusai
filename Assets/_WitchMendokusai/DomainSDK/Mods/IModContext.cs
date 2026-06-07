namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-188 — IMod.Initialize 가 받는 진입 context. 콘텐츠 등록 API seam.
	/// 후속 phase 에서 logger/metadata/permission 등 확장.
	/// </summary>
	public interface IModContext
	{
		IModContentRegistry ContentRegistry { get; }
	}
}
