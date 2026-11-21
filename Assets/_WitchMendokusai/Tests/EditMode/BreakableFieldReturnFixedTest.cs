using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// Play 종료 때 개발자 창 OnDestroy 가 진열장을 풀면 고정 배치 몸은 이미 파괴된 상태 (2026-11-21 MOIS MissingReferenceException)
	public class BreakableFieldReturnFixedTest
	{
		private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
		private BreakableField field;
		private HashSet<BreakableObject> fixedPlaced;
		private GameObject root;

		[SetUp]
		public void SetUp()
		{
			field = new BreakableField(null, null, null);
			FieldInfo rootField = typeof(BreakableField).GetField("root", PRIVATE);
			FieldInfo fixedField = typeof(BreakableField).GetField("fixedPlaced", PRIVATE);
			Assert.IsNotNull(rootField, "BreakableField.root 이름 바뀜");
			Assert.IsNotNull(fixedField, "BreakableField.fixedPlaced 이름 바뀜");
			root = ((Transform)rootField.GetValue(field)).gameObject;
			fixedPlaced = (HashSet<BreakableObject>)fixedField.GetValue(field);
		}

		[TearDown]
		public void TearDown()
		{
			if (root != null)
				Object.DestroyImmediate(root);
		}

		// PlaceFixed 는 Resources 설정과 프리팹이 필요해 명단과 셈을 직접 채움
		private BreakableObject PlaceDestroyedFixed()
		{
			GameObject body = new("Breakable");
			BreakableObject breakable = body.AddComponent<BreakableObject>();
			fixedPlaced.Add(breakable);
			typeof(BreakableField).GetProperty(nameof(BreakableField.ActiveCount)).SetValue(field, 1);
			Object.DestroyImmediate(body);
			return breakable;
		}

		[Test]
		public void Destroyed_fixed_breakable_is_forgotten_without_touching_object()
		{
			BreakableObject breakable = PlaceDestroyedFixed();

			Assert.DoesNotThrow(() => field.ReturnFixed(breakable));
			Assert.AreEqual(0, fixedPlaced.Count);
			Assert.AreEqual(0, field.ActiveCount);
		}

		[Test]
		public void Release_all_skips_destroyed_fixed_breakable()
		{
			PlaceDestroyedFixed();

			Assert.DoesNotThrow(() => field.ReleaseAll());
			Assert.AreEqual(0, fixedPlaced.Count);
			Assert.AreEqual(0, field.ActiveCount);
		}
	}
}
