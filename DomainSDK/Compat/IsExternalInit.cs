namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// <c>record</c> 와 <c>init</c> 접근자를 쓰게 해 주는 쉼(shim).
	///
	/// ★ <b>공개</b>여야 한다 (실측 2026-08-16). <c>internal</c> 로 두면 <b>어셈블리 경계를 못 넘어</b>
	///   조각을 낼 때마다 그 조각이 통째로 CS0518 로 선다(도메인을 asmdef 로 자르다 실제로 겪었다).
	///   조각마다 사본을 두는 길도 있지만 그건 같은 파일이 서른 벌 생기는 것이다.
	///
	/// ★ grep 으로는 <b>영영 안 보이는 의존</b>이다 — 타입 이름을 부르는 게 아니라
	///   컴파일러가 이 타입의 <b>존재</b>를 확인할 뿐이라서다.
	///   「어느 도메인이 어디에 기대나」를 정규식으로 재려던 시도가 여기서 무너졌다.
	///   경계를 재는 자는 컴파일러여야 한다.
	/// </summary>
	public static class IsExternalInit { }
}
