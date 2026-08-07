// 최소 NUnit 대역 — *실제 EditMode 시험 파일 자체*를 Unity 없이 컴파일·실행하기 위한 것.
// 이게 있어야 「시험을 썼다」가 아니라 「시험이 돈다」가 된다. Unity 직렬화는 여전히 미검증.
using System;
using System.Collections;
using System.Collections.Generic;

namespace NUnit.Framework
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class TestAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class SetUpAttribute : Attribute
	{
	}

	public sealed class AssertionException : Exception
	{
		public AssertionException(string message) : base(message)
		{
		}
	}

	public class Constraint
	{
		/// <summary>NUnit 의 `.AsCollection` — 이 대역에서는 비교 방식이 이미 컬렉션 동등이라 자기 자신.</summary>
		public Constraint AsCollection => this;

		private readonly Func<object, bool> predicate;
		private readonly string description;

		public Constraint(Func<object, bool> predicate, string description)
		{
			this.predicate = predicate;
			this.description = description;
		}

		public bool Matches(object actual) => predicate(actual);
		public string Description => description;
	}

	public sealed class NotBuilder
	{
		public Constraint Null => new(actual => actual != null, "not null");
		public Constraint True => new(actual => actual is bool b && b == false, "not true");
		public Constraint False => new(actual => actual is bool b && b, "not false");
		public Constraint Zero => new(actual => Convert.ToDouble(actual) != 0d, "not zero");
		public Constraint Empty => new(actual => Count(actual) != 0, "not empty");
		public Constraint EqualTo(object expected) => new(actual => AreEqual(actual, expected) == false, $"not equal to {expected}");
		public Constraint SameAs(object expected) => new(actual => ReferenceEquals(actual, expected) == false, "not same as");

		internal static bool AreEqual(object actual, object expected) => Is.AreEqualInternal(actual, expected);
		internal static int Count(object actual) => Is.CountInternal(actual);
	}

	public static class Is
	{
		public static NotBuilder Not => new();

		public static Constraint True => new(actual => actual is bool b && b, "true");
		public static Constraint False => new(actual => actual is bool b && b == false, "false");
		public static Constraint Null => new(actual => actual == null, "null");
		public static Constraint Zero => new(actual => Convert.ToDouble(actual) == 0d, "zero");
		public static Constraint Empty => new(actual => CountInternal(actual) == 0, "empty");

		public static Constraint EqualTo(object expected) => new(actual =>
		{
			// 배열·목록끼리는 「순서까지 같은가」로 본다 — 참조 비교로는 시험이 늘 빨개진다.
			if (actual is IEnumerable actualItems && expected is IEnumerable expectedItems
				&& actual is string == false && expected is string == false)
			{
				List<object> left = new();
				foreach (object item in actualItems) { left.Add(item); }
				List<object> right = new();
				foreach (object item in expectedItems) { right.Add(item); }
				if (left.Count != right.Count) { return false; }
				for (int i = 0; i < left.Count; i++)
				{
					if (AreEqualInternal(left[i], right[i]) == false) { return false; }
				}
				return true;
			}
			return AreEqualInternal(actual, expected);
		}, $"equal to {Format(expected)}");
		public static Constraint SameAs(object expected) => new(actual => ReferenceEquals(actual, expected), "same instance");
		public static Constraint EquivalentTo(IEnumerable expected) => new(actual =>
		{
			List<object> actualItems = ToList(actual);
			List<object> expectedItems = ToList(expected);
			if (actualItems.Count != expectedItems.Count)
			{
				return false;
			}
			for (int i = 0; i < expectedItems.Count; i++)
			{
				int matchIndex = actualItems.FindIndex(item => AreEqualInternal(item, expectedItems[i]));
				if (matchIndex < 0)
				{
					return false;
				}
				actualItems.RemoveAt(matchIndex);
			}
			return true;
		}, "equivalent collection");

		private static List<object> ToList(object value)
		{
			List<object> items = new();
			if (value is IEnumerable enumerable)
			{
				foreach (object item in enumerable)
				{
					items.Add(item);
				}
			}
			return items;
		}

		public static Constraint GreaterThan(object expected) => new(actual => Convert.ToDouble(actual) > Convert.ToDouble(expected), $"> {expected}");
		public static Constraint LessThan(object expected) => new(actual => Convert.ToDouble(actual) < Convert.ToDouble(expected), $"< {expected}");

		internal static bool AreEqualInternal(object actual, object expected)
		{
			if (actual == null || expected == null)
			{
				return ReferenceEquals(actual, expected);
			}
			if (IsNumeric(actual) && IsNumeric(expected))
			{
				return Math.Abs(Convert.ToDouble(actual) - Convert.ToDouble(expected)) < 1e-6;
			}
			return actual.Equals(expected);
		}

		private static bool IsNumeric(object value) =>
			value is int || value is long || value is float || value is double || value is short || value is byte;

		internal static int CountInternal(object actual)
		{
			if (actual is ICollection collection)
			{
				return collection.Count;
			}
			int count = 0;
			if (actual is IEnumerable enumerable)
			{
				foreach (object _ in enumerable)
				{
					count++;
				}
			}
			return count;
		}

		internal static string Format(object value) => value == null ? "null" : value.ToString();
	}

	public static class Throws
	{
		public static Constraint TypeOf<T>() where T : Exception =>
			new(actual => actual is T, $"throws {typeof(T).Name}");

		public static Constraint InstanceOf<T>() where T : Exception =>
			new(actual => actual is T, $"throws {typeof(T).Name} (or subclass)");

		public static Constraint Nothing => new(actual => actual == null, "nothing thrown");

		// 진짜 NUnit 에 있는 지름길들. 시험 파일이 **저장소의 진짜 파일**이므로, 여기 없으면
		// 「Unity 에서는 되는데 하네스에서만 안 되는」 문법이 생긴다 — 그러면 시험을 하네스에 맞춰
		// 비틀게 되고, 그게 쌓이면 하네스가 진짜 시험을 못 돌린다. 없는 것만 그때그때 채운다.
		public static Constraint InvalidOperationException => TypeOf<InvalidOperationException>();

		public static Constraint ArgumentException => TypeOf<ArgumentException>();

		public static Constraint ArgumentNullException => TypeOf<ArgumentNullException>();
	}

	public static class Assert
	{
		public static void That(object actual, Constraint constraint, string message = null)
		{
			if (constraint.Matches(actual))
			{
				return;
			}
			throw new AssertionException($"expected {constraint.Description}, was {Is.Format(actual)}{(message == null ? "" : " — " + message)}");
		}

		public static void That(Func<object> code, Constraint constraint, string message = null)
		{
			That(CaptureException(() => code()), constraint, message);
		}

		public static void That(Action code, Constraint constraint, string message = null)
		{
			That(CaptureException(code), constraint, message);
		}

		public static void Fail(string message) => throw new AssertionException(message);

		private static Exception CaptureException(Action code)
		{
			try
			{
				code();
			}
			catch (Exception thrown)
			{
				return thrown;
			}
			return null;
		}
	}
}
