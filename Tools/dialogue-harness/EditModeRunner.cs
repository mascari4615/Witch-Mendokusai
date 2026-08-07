// 실제 EditMode 시험 파일(tests/ 에 복사된 원본 그대로)을 반사로 찾아 실행한다.
// = 「시험을 썼다」가 아니라 「시험이 실제로 돈다」. ※ Unity 직렬화·에디터 거동은 여전히 미검증.
using System;
using System.Reflection;
using NUnit.Framework;

internal static class EditModeRunner
{
	public static (int passed, int failed) RunAll()
	{
		int passed = 0;
		int failed = 0;

		Type[] types = Assembly.GetExecutingAssembly().GetTypes();
		Array.Sort(types, (a, b) => string.CompareOrdinal(a.Name, b.Name));

		foreach (Type type in types)
		{
			if (type.Namespace == null || type.Namespace.EndsWith(".Tests", StringComparison.Ordinal) == false)
			{
				continue;
			}

			MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
			Array.Sort(methods, (a, b) => string.CompareOrdinal(a.Name, b.Name));

			foreach (MethodInfo method in methods)
			{
				if (method.GetCustomAttribute<TestAttribute>() == null)
				{
					continue;
				}

				string label = $"{type.Name}.{method.Name}";
				try
				{
					object instance = Activator.CreateInstance(type);
					method.Invoke(instance, null);
					passed++;
					Console.WriteLine($"  PASS  {label}");
				}
				catch (TargetInvocationException invocation)
				{
					failed++;
					Console.WriteLine($"  FAIL  {label} — {invocation.InnerException?.Message}");
				}
				catch (Exception other)
				{
					failed++;
					Console.WriteLine($"  FAIL  {label} — {other.GetType().Name}: {other.Message}");
				}
			}
		}

		return (passed, failed);
	}
}
