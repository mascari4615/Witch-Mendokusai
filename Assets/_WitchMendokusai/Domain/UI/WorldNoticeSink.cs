using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계가 보낸 짧은 말을 <b>화면에 띄운다</b> (TASK-WM-217).
	///
	/// ★ 왜 Domain 인가: 통신 층에서는 UI 를 볼 수 없다(asmdef 단방향) — 가방과 같은 이유다.
	///   그래서 보이는 쪽이 구멍을 채우고, 통신 층은 부르기만 한다.
	/// </summary>
	public sealed class WorldNoticeSink : IWorldNoticeReceiver
	{
		/// <summary>스스로 꽂힌다 — 씬에 얹어야만 도는 구조면 조용히 아무 말도 안 나온다.</summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			WorldNoticeBridge.RegisterReceiver(new WorldNoticeSink());
		}

		public void ShowWorldNotice(string message)
		{
			if (UIManager.TryGetExistingInstance(out UIManager ui) == false)
			{
				// 화면이 아직 없으면 로그로라도 남긴다 — 조용히 사라지는 것보다 낫다.
				Debug.Log($"[world] {message}");
				return;
			}

			ui.PopText(message, TextType.Warning);
		}
	}
}
