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
			if (target is GameObject gameObject)
			{
				gameObject.transform.destroyed = true;
			}
		}

		public static void Destroy(Object target) => DestroyImmediate(target);
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
		/// 붙이는 시늉을 하고 **Awake 까지 불러 준다.**
		///
		/// ★ 왜 있어야 하나: 이게 있으면 러너 시험을 **유니티와 하네스 양쪽에서 같은 글로** 쓸 수 있다.
		///   없으면 하네스 전용 시험을 따로 쓰게 되고, 그건 CI 에서 안 돈다.
		///
		/// ★ 왜 Awake 를 부르나: 안 부르면 **Awake 에서 하는 배선이 아무 검사도 못 받는다.**
		///   유니티에서는 붙는 즉시 도는 코드인데 하네스에서만 안 돌면, 두 쪽이 다른 물건이 된다.
		///   (자체 리뷰가 짚어 준 자리다 — 「유일하게 돌릴 수 있는 검사가 그 배선을 안 본다」.)
		///   Start/OnEnable 은 아직 안 부른다 — 필요해지면 그때 같은 자리에 더한다.
		/// </summary>
		public T AddComponent<T>() where T : MonoBehaviour, new()
		{
			T component = new();
			System.Reflection.MethodInfo awake = typeof(T).GetMethod(
				"Awake",
				System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic);
			if (awake != null && awake.GetParameters().Length == 0)
			{
				awake.Invoke(component, null);
			}
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
