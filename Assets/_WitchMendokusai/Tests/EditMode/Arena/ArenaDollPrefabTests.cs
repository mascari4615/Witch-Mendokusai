using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// `ArenaDoll.prefab` 이 「플레이어 전용 부품」을 달고 있지 않은지 지킨다.
	///
	/// ★ 왜 (2026-08-06 실측): `PlayerKnockbackCameraGlue` 가 이 프리팹에 붙어 있었다.
	///   TASK-WM-165 item 11 은 「CameraGlue 제거」를 완료로 적어놨는데 자산엔 남아 있었다.
	///
	///   ⚠ **문서가 거짓말한 게 아니다 — 지운 게 되살아난 것이다.** 원인은
	///   `PlayerObject` 에 붙어 있던 `[RequireComponent(typeof(PlayerKnockbackCameraGlue))]` 다.
	///   투기장 인형도 `PlayerObject` 를 쓰므로(로스터 스폰이 요구), **지워도 Unity 가 임포트 때 도로 붙인다.**
	///   증거: 제거 대상 다섯 중 `RequireComponent` 가 걸린 건 **이것 하나뿐**이고, 나머지 넷
	///   (`Player`/`PlayerRotation`/`LookAtScreenCenter`/`PlayerLandingCameraGlue`)은 **전부 지워진 채 남아 있다.**
	///   되살아난 것도 정확히 하나다 = 우연이 아니라 이 attribute 가 원인이다.
	///
	///   그래서 진짜 고침은 프리팹이 아니라 **그 요구사항을 뗀 것**이다(`PlayerObject.cs` 주석 참조).
	///   프리팹만 고치는 건 영원히 안 먹힌다 — 나도 그걸 세 번 돌려보고 나서야 알았다.
	///
	///   무엇이 깨지나: 저 글루는 `UnitHealth.OnTakeDamage` 마다 `CameraManager.GenerateCameraImpulse`
	///   를 때린다 — **전역 카메라**다. 투기장엔 인형이 팀마다 여럿 서고, 풀 스폰은
	///   `ObjectPoolManager` 가 `InjectGameObject` 로 DI 를 넣어주므로 `cameraManager` 가 null 도
	///   아니다(= 조용히 죽어있지 않고 **실제로 발동한다**). 게다가 `minimumAmplitude = 0.18` 이라
	///   넉백 0 인 타격도 최소 흔들림이 보장된다. 실측 매치 하나가 hits=11 이었다 =
	///   관전 카메라가 인형 피격마다 11번 흔들린다.
	///
	/// ★ 그래서 「이번에도 손으로 떼고 끝」이 아니라 **기계 검사**로 바꾼다.
	///   손 제거는 한 번 더 흘릴 수 있고, `Player.prefab` 에 새 부품이 붙으면 그게 또 따라온다.
	///   이 테스트는 *다음* 부품까지 잡는다.
	/// </summary>
	public sealed class ArenaDollPrefabTests
	{
		private const string ARENA_DOLL_PATH = "Assets/_WitchMendokusai/Domain/Doll/_Common/ArenaDoll.prefab";

		/// <summary>
		/// 투기장 인형에 있으면 안 되는 부품들. 근거는 TASK-WM-165 item 11 「남은 것」 계획.
		/// - Player / PlayerRotation / PlayerAim : 입력 구동 이동·조준 → `TacticDriver` 와 이동 채널 경쟁
		/// - Player*CameraGlue                  : 단일 플레이어용 전역 카메라 임펄스
		/// - LookAtScreenCenter                 : 마우스 화면 중심 조준(투기장엔 조종자가 없다)
		/// - StudioListener                     : FMOD 리스너 중복(유닛마다 하나씩 생기면 오디오가 깨진다)
		/// </summary>
		private static readonly string[] FORBIDDEN_COMPONENTS =
		{
			"Player",
			"PlayerRotation",
			"PlayerAim",
			"PlayerKnockbackCameraGlue",
			"PlayerLandingCameraGlue",
			"LookAtScreenCenter",
			"StudioListener",
		};

		/// <summary>
		/// 반대로 **떼면 안 되는** 부품. 과잉 삭제로 스폰이 죽는 걸 막는다
		/// (로스터 스폰은 `PlayerObject` 를 요구한다 — item 11 BLOCKER 기록).
		/// </summary>
		private static readonly string[] REQUIRED_COMPONENTS =
		{
			"PlayerObject",
			"UnitMovement",
			"UnitHealth",
		};

		private static GameObject LoadArenaDoll()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ARENA_DOLL_PATH);

			// ★ 「못 찾음 = 통과」 금지. 프리팹이 옮겨졌는데 초록불이 뜨면 이 검사는 죽은 것이다.
			Assert.IsNotNull(
				prefab,
				$"ArenaDoll 프리팹을 못 찾았다: {ARENA_DOLL_PATH}\n" +
				"위반이 없는 게 아니라 아무것도 검사하지 않은 것이다 — 경로가 바뀌었으면 상수를 갱신할 것.");

			return prefab;
		}

		private static List<string> CollectComponentNames(GameObject prefab, out int missingScriptCount)
		{
			List<string> names = new List<string>();
			missingScriptCount = 0;

			foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
			{
				// null = 스크립트가 깨진(missing) 칸. 프리팹을 손으로 편집하면 나올 수 있어 따로 센다.
				if (component == null)
				{
					missingScriptCount++;
					continue;
				}

				names.Add(component.GetType().Name);
			}

			return names;
		}

		[Test]
		public void ArenaDoll_플레이어전용_부품을_달고있지_않다()
		{
			GameObject prefab = LoadArenaDoll();
			List<string> names = CollectComponentNames(prefab, out int missingScriptCount);

			List<string> found = new List<string>();
			foreach (string forbidden in FORBIDDEN_COMPONENTS)
			{
				if (names.Contains(forbidden))
					found.Add(forbidden);
			}

			if (found.Count > 0)
			{
				StringBuilder message = new StringBuilder();
				message.AppendLine("ArenaDoll 에 플레이어 전용 부품이 남아 있다:");
				foreach (string name in found)
					message.AppendLine($"  - {name}");
				message.AppendLine();
				message.AppendLine("투기장 인형은 TacticDriver 가 몬다. 입력/카메라/리스너 부품은 떼야 한다.");
				message.AppendLine($"현재 붙은 부품 전체: {string.Join(", ", names)}");

				// ── 진단: 「파일엔 없는데 Unity 엔 있다」를 가른다 ─────────────────────────
				// 밖에서 grep 하면 이 프리팹엔 글루 스크립트 guid 가 0건인데 이 검사는 계속 있다고
				// 답했다(강제 전체 재임포트 후에도). 그렇다면 *Unity 가 읽는 파일* 이 내가 보는
				// 파일과 다른 것이다 — 추측 대신 Unity 안에서 직접 파일을 읽어 확인한다.
				const string GLUE_SCRIPT_GUID = "5081210ff2a42bb48a17dff0e2bae1cd";
				string projectRoot = Directory.GetParent(Application.dataPath).FullName;
				string fullPath = Path.Combine(projectRoot, ARENA_DOLL_PATH.Replace('/', Path.DirectorySeparatorChar));

				message.AppendLine();
				message.AppendLine("── 진단 ──");
				message.AppendLine($"projectRoot   : {projectRoot}");
				message.AppendLine($"파일 존재     : {File.Exists(fullPath)}");

				if (File.Exists(fullPath))
				{
					string text = File.ReadAllText(fullPath);
					message.AppendLine($"파일 크기     : {text.Length}");
					message.AppendLine($"최종 수정     : {File.GetLastWriteTime(fullPath):HH:mm:ss}");
					bool fileHasGlue = text.Contains(GLUE_SCRIPT_GUID);
					message.AppendLine($"글루 guid 포함: {fileHasGlue}");

					if (fileHasGlue == false)
					{
						// ★ 여기 오면 캐시를 파지 말 것 — 2026-08-06 에 내가 그러다 Library 를 통째로 날렸다.
						//   파일에 없는 부품이 조립된 프리팹에 있다 = **누군가 붙이고 있다**는 뜻이다.
						//   캐시는 *옛 값*을 보여줄 뿐 **없는 값을 만들어내지 않는다.**
						message.AppendLine();
						message.AppendLine("→ 파일엔 없는데 Unity 가 들고 있다. **캐시 문제가 아니다.**");
						message.AppendLine("  누가 이 부품을 도로 붙이는지 찾을 것. 1순위 용의자 = `[RequireComponent]`:");
						message.AppendLine("    grep -rn \"RequireComponent.*PlayerKnockbackCameraGlue\" --include=*.cs");
						message.AppendLine("  (실제 전례: `PlayerObject` 가 이걸 요구해서 임포트마다 되살아났다.)");
					}
				}

				Assert.Fail(message.ToString());
			}

			Assert.AreEqual(
				0,
				missingScriptCount,
				$"ArenaDoll 에 깨진 스크립트 칸이 {missingScriptCount}개 있다 — 프리팹 수동 편집이 참조를 남겼다.");
		}

		/// <summary>
		/// ★ 이 테스트가 없으면 위 테스트를 **틀린 방향으로** 통과시킬 수 있다:
		///   `Player.prefab` 에서도 글루를 떼면 빨간 불은 사라지지만 **진짜 플레이어가 피격감을 잃는다.**
		///   그 글루의 존재 이유가 「맞은 느낌의 기본선」이므로(컴포넌트 주석), 여기 있는 건 **정상**이다.
		///   비대칭(플레이어=있음 / 투기장 인형=없음)이 곧 의도다 — 그 의도를 기계가 붙잡는다.
		/// </summary>
		[Test]
		public void 진짜_플레이어는_카메라글루를_그대로_갖는다()
		{
			const string PLAYER_PATH = "Assets/_WitchMendokusai/Domain/Doll/_Common/Player.prefab";
			GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PATH);

			Assert.IsNotNull(
				player,
				$"Player 프리팹을 못 찾았다: {PLAYER_PATH} — 경로가 바뀌었으면 상수를 갱신할 것(못 찾음은 통과가 아니다).");

			List<string> names = CollectComponentNames(player, out int _);

			Assert.IsTrue(
				names.Contains("PlayerKnockbackCameraGlue"),
				"Player.prefab 에서 PlayerKnockbackCameraGlue 가 사라졌다.\n" +
				"투기장 인형에서 뗀 것을 진짜 플레이어에서까지 떼면 피격 카메라 흔들림이 죽는다 — " +
				"떼야 할 곳은 ArenaDoll 쪽뿐이다.");
		}

		[Test]
		public void ArenaDoll_전투에_필요한_부품은_남아있다()
		{
			GameObject prefab = LoadArenaDoll();
			List<string> names = CollectComponentNames(prefab, out int _);

			foreach (string required in REQUIRED_COMPONENTS)
			{
				Assert.IsTrue(
					names.Contains(required),
					$"ArenaDoll 에 `{required}` 가 없다 — 과잉 삭제다.\n" +
					$"현재 붙은 부품 전체: {string.Join(", ", names)}");
			}
		}
	}
}
