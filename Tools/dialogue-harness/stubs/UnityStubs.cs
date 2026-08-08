// 순수 로직 하네스용 최소 스텁 — Unity 없이 대화 그래프 traversal 을 돌리기 위한 것.
// ※ 이것은 Unity 컴파일의 대체물이 아니다. 직렬화·에디터 거동은 여기서 전혀 검증되지 않는다.
using System;

namespace UnityEngine
{
	/// <summary>
	/// 유니티의 「파괴된 객체는 null 처럼 보인다」 규칙까지 흉내낸다 — 그 규칙을 빼면
	/// 죽은 참조를 다루는 코드가 하네스에서만 통과한다(가장 위험한 거짓 초록).
	/// </summary>
	public class Object
	{
		public string name { get; set; } = string.Empty;

		internal bool destroyed;

		public static bool operator ==(Object left, Object right)
		{
			bool leftNull = left is null || left.destroyed;
			bool rightNull = right is null || right.destroyed;
			if (leftNull || rightNull)
			{
				return leftNull && rightNull;
			}
			return ReferenceEquals(left, right);
		}

		public static bool operator !=(Object left, Object right) => (left == right) == false;

		public override bool Equals(object other) => ReferenceEquals(this, other);
		public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

		public static void DestroyImmediate(Object target)
		{
			if (target is null)
			{
				return;
			}
			target.destroyed = true;
			if (target is MonoBehaviour behaviour)
			{
				InvokeLifecycle(behaviour, "OnDestroy");
				return;
			}
			if (target is GameObject gameObject)
			{
				gameObject.transform.destroyed = true;
				// 게임 오브젝트를 없애면 붙은 것들의 뒷정리도 돈다 — 유니티가 그렇게 한다.
				// 안 돌리면 「없앨 때 하는 정리」가 검사에서 통째로 빠진다.
				for (int i = 0; i < gameObject.Components.Count; i++)
				{
					MonoBehaviour attached = gameObject.Components[i];
					if (attached.destroyed)
					{
						continue;
					}
					attached.destroyed = true;
					InvokeLifecycle(attached, "OnDestroy");
				}
			}
		}

		public static void Destroy(Object target) => DestroyImmediate(target);

		/// <summary>이름으로 수명주기 메서드를 부른다 — 유니티가 리플렉션으로 부르는 것과 같은 결.</summary>
		internal static void InvokeLifecycle(MonoBehaviour behaviour, string methodName)
		{
			System.Reflection.MethodInfo method = behaviour.GetType().GetMethod(
				methodName,
				System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic);
			if (method != null && method.GetParameters().Length == 0)
			{
				method.Invoke(behaviour, null);
			}
		}
	}

	public class Transform : Object
	{
		public GameObject gameObject { get; internal set; }
	}

	public class GameObject : Object
	{
		public Transform transform { get; }

		public GameObject(string name = "")
		{
			this.name = name;
			transform = new Transform { name = name, gameObject = this };
		}

		/// <summary>
		/// 붙이는 시늉만 한다 — **Awake 는 부르지 않는다.**
		///
		/// ★ 예전엔 여기서 Awake 를 불러 줬다. 의도는 좋았지만 결과가 나빴다: 진짜 유니티는
		///   EditMode 에서 Awake 를 안 돌려주는데 흉내만 돌려주니, **흉내가 진짜보다 더 살아 있는**
		///   상태가 됐다. 그래서 하네스 255개 전부 초록인 채로 유니티 쪽 대화 시험 둘이 넘어져
		///   있었다(2026-08-08). 초록이 「된다」가 아니라 「흉내에서만 된다」를 뜻하게 된 것이다.
		///
		/// ★ 지금은 시험이 <c>DialogueTestHost.Attach</c> 로 **직접** 돌린다 — 양쪽 세계가 같은 글로
		///   같은 것을 본다. 배선 검사가 사라진 게 아니라, 부르는 자리가 시험 쪽으로 옮겨졌다.
		/// </summary>
		public System.Collections.Generic.List<MonoBehaviour> Components { get; } = new();

		public T AddComponent<T>() where T : MonoBehaviour, new()
		{
			T component = new();
			Components.Add(component);
			component.AttachTo(this);
			return component;
		}
	}

	public class ScriptableObject : Object
	{
		public static T CreateInstance<T>() where T : ScriptableObject
		{
			return (T)Activator.CreateInstance(typeof(T));
		}
	}

	public static class Mathf
	{
		public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
		public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
		public static float Clamp01(float value) => Clamp(value, 0f, 1f);
		public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
		public static float Abs(float value) => value < 0f ? -value : value;
		public static float Max(float a, float b) => a > b ? a : b;
		public static float Min(float a, float b) => a < b ? a : b;
		public static float Pow(float value, float power) => (float)Math.Pow(value, power);
		public static float Sqrt(float value) => (float)Math.Sqrt(value);
		public static int RoundToInt(float value) => (int)Math.Round(value);
		public static float InverseLerp(float a, float b, float value) => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((value - a) / (b - a));
	}

	public class Sprite : Object
	{
	}

	public class TextAsset : Object
	{
		public string text { get; }

		public TextAsset(string text)
		{
			this.text = text;
		}
	}

	public class AudioClip : Object
	{
	}

	public struct Vector2
	{
		public float x;
		public float y;

		public Vector2(float x, float y)
		{
			this.x = x;
			this.y = y;
		}
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class SerializeField : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class SerializeReference : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class HeaderAttribute : Attribute
	{
		public HeaderAttribute(string header)
		{
		}
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class TextAreaAttribute : Attribute
	{
		public TextAreaAttribute()
		{
		}

		public TextAreaAttribute(int minLines, int maxLines)
		{
		}
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class TooltipAttribute : Attribute
	{
		public TooltipAttribute(string tooltip)
		{
		}
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class RangeAttribute : Attribute
	{
		public RangeAttribute(float min, float max)
		{
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class CreateAssetMenuAttribute : Attribute
	{
		public string fileName;
		public string menuName;
		public int order;
	}
}

namespace UnityEngine.Serialization
{
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public sealed class FormerlySerializedAsAttribute : Attribute
	{
		public FormerlySerializedAsAttribute(string oldName)
		{
		}
	}
}

namespace WitchMendokusai
{
	/// <summary>실제 DataSO 의 최소 대역 — 대화 traversal 이 쓰는 표면(SO 로서 존재)만.</summary>
	public abstract class DataSO : UnityEngine.ScriptableObject
	{
		public const int NONE_ID = -1;

		public int ID { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
	}
}
