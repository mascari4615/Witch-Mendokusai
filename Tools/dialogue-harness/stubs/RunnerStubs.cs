using System.Collections;
using System.Collections.Generic;

// 러너를 컴파일하기 위한 최소 대역들.
//
// ★ 왜 여기까지 하나: 러너는 이 하네스 밖에 있어서 **아무 검사도 안 받고 있었다.**
//   같은 작업에서 새 메서드를 추가하고 러너에서 부르면, 단일 파일 검사기는 「그런 멤버 없다」고 하고
//   (아직 빌드 안 된 어셈블리를 보니까) 하네스는 파일 자체를 안 문다 — **양쪽 다 못 보는 구멍**이었다.
//   오늘 그 구멍으로 네 파일이 지나갔다.
//
// ★ 이 대역들의 위험: 여기 적은 모양은 **게임 쪽 진짜 타입에 대한 주장**이다.
//   진짜가 바뀌면 하네스는 그대로 초록인데 유니티 빌드만 깨진다(대역 드리프트).
//   그래서 **러너가 실제로 쓰는 것만** 적는다. 남는 멤버를 미리 만들어 두면 드리프트가 늘 뿐이다.
//   최종 판단은 언제나 유니티 컴파일·CI 다 — 여기서 잡는 건 「오타·시그니처 어긋남」 층이다.
namespace UnityEngine
{
	public class MonoBehaviour : Object
	{
		private GameObject host;

		public Transform transform => gameObject.transform;

		/// <summary>붙은 곳. 안 붙였으면 혼자 있는 것으로 친다(직접 new 로 만든 경우).</summary>
		public GameObject gameObject => host ??= new GameObject();

		internal void AttachTo(GameObject owner)
		{
			host = owner;
		}

		public Coroutine StartCoroutine(IEnumerator routine) => new();

		public void StopCoroutine(Coroutine routine)
		{
		}
	}

	public sealed class Coroutine
	{
	}

	public sealed class Camera : Object
	{
		public static Camera main => null;

		public Transform transform { get; } = new();
	}

	/// <summary>러너의 경고·오류 통지. 하네스에서는 **삼킨다** — 여기서 재는 건 「컴파일되나」지 「무슨 말을 하나」가 아니다.</summary>
	public static class Debug
	{
		public static void Log(object message)
		{
		}

		public static void Log(object message, Object context)
		{
		}

		public static void LogWarning(object message)
		{
		}

		public static void LogWarning(object message, Object context)
		{
		}

		public static void LogError(object message)
		{
		}

		public static void LogError(object message, Object context)
		{
		}
	}

	public static class Time
	{
		public static float deltaTime => 0f;
	}

	public sealed class WaitForSeconds
	{
		public WaitForSeconds(float seconds)
		{
		}
	}
}

namespace VContainer
{
	[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Property | System.AttributeTargets.Field)]
	public sealed class InjectAttribute : System.Attribute
	{
	}
}

namespace WitchMendokusai
{
	public sealed class SpeechBubble
	{
		public void Show(UnityEngine.Transform anchor, string text, float duration)
		{
		}
	}

	public sealed class UIManager
	{
		public SpeechBubble SpeechBubble { get; }
	}

	public sealed class Inventory
	{
		public int GetItemAmount(int itemId) => 0;
	}

	public sealed class SOManager
	{
		public Inventory ItemInventory { get; }
	}

	public sealed class QuestManager
	{
		public IReadOnlyDictionary<int, QuestState> GetQuestStates() => new Dictionary<int, QuestState>();
	}
}
