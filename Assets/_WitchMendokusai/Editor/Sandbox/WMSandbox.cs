using System.Linq;
using UnityEngine;

namespace WitchMendokusai.Sandbox
{
	// 비-GUI 진입점(Unity CLI eval / 스크립트). GUI 없는 격리 프리뷰(에디트 모드, Play 미사용)
	// Ops 인터페이스 = 비-GUI 필수 룰 정합 — Gallery 창(수동)의 코드 짝.
	public static class WMSandbox
	{
		// 이름으로 데모를 격리 무대에 띄움(에디트 모드 라이브). 못 찾으면 등록목록 로그 + null.
		public static GameObject Open(string title)
		{
			ISandboxDemo demo = SandboxRegistry.Find(title);
			if (demo == null)
			{
				Debug.LogError($"[Sandbox] 데모 없음: '{title}'.\n등록:\n{List()}");
				return null;
			}

			return SandboxStage.Open(demo);
		}

		// 열린 무대 정리.
		public static void Close()
		{
			SandboxStage.Close();
		}

		// 등록된 데모 나열(Unity CLI로 확인)
		public static string List()
		{
			return string.Join("\n", SandboxRegistry.Discover().Select(demo => $"[{demo.Category}] {demo.Title}"));
		}
	}
}
