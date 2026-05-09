using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 명령 인터페이스. 새 명령 추가 = 구현체 작성 + DevCommandRegistry.Register 한 줄.
	/// 모든 데브 액션(아이템 지급/몬스터 스폰/던전 이동 등)은 이 인터페이스를 거친다 — single source of truth.
	/// </summary>
	public interface IDevCommand
	{
		/// <summary>명령 식별자. 입력에서 첫 토큰. 예: "give"</summary>
		string Name { get; }

		/// <summary>한 줄 사용법. help 명령이 출력. 예: "give &lt;itemRef&gt; [amount]"</summary>
		string Usage { get; }

		/// <summary>명령 실행. 예외는 위에서 catch — 여기선 throw 자유롭게.</summary>
		void Execute(DevCommandContext context, string[] args);

		/// <summary>Tab 자동완성 후보. partial = 현재까지 입력된 토큰 배열 (마지막은 미완성).</summary>
		IEnumerable<string> Suggest(string[] partial);
	}
}
