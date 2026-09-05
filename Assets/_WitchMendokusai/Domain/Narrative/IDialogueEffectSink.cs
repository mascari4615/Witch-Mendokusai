using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화가 「무언가를 일으키는」 통로 (TASK-WM-052).
	///
	/// ★ 왜 <see cref="IEffectRunner"/> 를 직접 안 쓰나: 그 인터페이스는 대화가 쓸 일 없는 것들
	///   (`BindDataManager`, `EffectInfoData` 오버로드)까지 들고 있다. 좁은 구멍 하나만 뚫으면
	///   재생 상태기가 그 큰 표면 없이 서고, 시험에서 가짜로 바꿔 끼우기도 한 줄이다
	///   (= 대화 로직 검증이 DI 컨테이너를 안 끌고 온다).
	///
	/// 이 파일에 구현을 같이 두지 않는 이유: 구현은 게임 쪽(<see cref="IEffectRunner"/>)에 매달리는데,
	/// 그러면 **이 인터페이스만 따로 컴파일해 볼 수가 없다**(파일 단위 컴파일 도구 실측, 2026-08-08).
	/// 순수한 쪽과 게임에 매달리는 쪽을 파일로 갈라 두면 검증 단위가 작아진다.
	/// </summary>
	public interface IDialogueEffectSink
	{
		/// <summary>인스펙터에서 자산을 직접 물린 효과.</summary>
		void Apply(IReadOnlyList<EffectInfo> effects);

		/// <summary>
		/// **글로 적은** 효과 — 자산 대신 번호로 가리킨다(`!아이템 1001 3`).
		/// 번호 → 자산 찾기는 게임 쪽이 이미 하는 일이라, 여기서 흉내내지 않고 그대로 넘긴다.
		/// </summary>
		void ApplyData(IReadOnlyList<EffectInfoData> effects);
	}
}
