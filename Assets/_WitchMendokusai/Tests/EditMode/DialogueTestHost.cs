using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-211 — 시험용으로 컴포넌트를 붙이고 <b>Awake 를 손으로 돌려 준다.</b>
	///
	/// ★ 왜 손으로 부르나: 유니티는 **EditMode 에서 Awake 를 돌려주지 않는다**(플레이 중이 아니니까).
	///   그래서 「붙는 즉시 도는 배선」은 EditMode 시험에서 저절로는 절대 검사받지 못한다.
	///
	/// ★ 왜 여기 한 자리인가: 유니티 없이 도는 하네스 쪽 흉내가 붙일 때마다 Awake 를 대신 불러 주고
	///   있었다. 그래서 하네스는 초록인데 진짜 유니티는 빨간 상태가 됐다 — 실제로 그 모양으로
	///   main 의 대화 시험 둘이 넘어져 있었다(2026-08-08). 흉내가 진짜보다 더 살아 있으면
	///   초록은 「된다」가 아니라 「흉내에서만 된다」는 뜻이 된다.
	///   이제 두 세계 모두 여기서만 부른다 — 같은 글로 같은 것을 본다.
	/// </summary>
	public static class DialogueTestHost
	{
		/// <summary>새 오브젝트에 붙이고 Awake 까지 돌린 컴포넌트를 준다.</summary>
		public static T Attach<T>(string hostName) where T : MonoBehaviour, new()
		{
			return Attach<T>(new GameObject(hostName));
		}

		/// <summary>이미 있는 오브젝트에 붙이고 Awake 까지 돌린다.</summary>
		public static T Attach<T>(GameObject host) where T : MonoBehaviour, new()
		{
			T component = host.AddComponent<T>();
			InvokeAwake(component);
			return component;
		}

		/// <summary>이름으로 찾아 부른다 — 유니티가 수명주기를 부르는 것과 같은 결(private 이어도 부른다).</summary>
		public static void InvokeAwake(MonoBehaviour behaviour)
		{
			System.Reflection.MethodInfo method = behaviour.GetType().GetMethod(
				"Awake",
				System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic);
			if (method != null && method.GetParameters().Length == 0)
			{
				method.Invoke(behaviour, null);
			}
		}
	}
}
