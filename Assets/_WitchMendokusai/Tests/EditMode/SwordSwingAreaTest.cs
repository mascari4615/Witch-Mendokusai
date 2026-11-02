using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 검 휘두르기 연출이 읽는 판정 범위 측정 회귀
	public sealed class SwordSwingAreaTest
	{
		private const float TOLERANCE = 0.001f;
		private const string HITBOX_PATH = "Assets/_WitchMendokusai/Domain/Crafting/Equipment/Weapon/Sword/SwordSlashBullet.prefab";

		[Test]
		public void FromHitbox_MeasuresBoxInAttackSpace()
		{
			GameObject root = new("Hitbox");
			try
			{
				root.transform.SetPositionAndRotation(new Vector3(5, 0, -3), Quaternion.LookRotation(Vector3.left));
				GameObject child = new("Box");
				child.transform.SetParent(root.transform, false);
				child.transform.localPosition = new Vector3(0, 0, 2);
				BoxCollider box = child.AddComponent<BoxCollider>();
				box.size = new Vector3(1, 1, 2);

				SwordSwingArea area = SwordSwingArea.FromHitbox(root.transform);

				Assert.That(area.InnerRadius, Is.EqualTo(1f).Within(TOLERANCE));
				Assert.That(area.OuterRadius, Is.EqualTo(new Vector2(0.5f, 3f).magnitude).Within(TOLERANCE));
				Assert.That(area.HalfAngle, Is.EqualTo(Mathf.Atan2(0.5f, 1f) * Mathf.Rad2Deg).Within(TOLERANCE));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FromHitbox_WiderBoxWidensArea()
		{
			GameObject root = new("Hitbox");
			try
			{
				BoxCollider box = root.AddComponent<BoxCollider>();
				box.center = new Vector3(0, 0, 1);
				box.size = new Vector3(1, 1, 1);
				SwordSwingArea narrow = SwordSwingArea.FromHitbox(root.transform);
				box.size = new Vector3(3, 1, 1);
				SwordSwingArea wide = SwordSwingArea.FromHitbox(root.transform);

				Assert.That(wide.HalfAngle, Is.GreaterThan(narrow.HalfAngle));
				Assert.That(wide.OuterRadius, Is.GreaterThan(narrow.OuterRadius));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void SwordHitboxPrefab_ReachesBeyondPlayer()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HITBOX_PATH);
			Assert.That(prefab, Is.Not.Null, HITBOX_PATH);
			GameObject instance = Object.Instantiate(prefab);
			try
			{
				SwordSwingArea area = SwordSwingArea.FromHitbox(instance.transform);
				Assert.That(area.OuterRadius, Is.GreaterThan(area.InnerRadius));
				Assert.That(area.HalfAngle, Is.GreaterThan(0f));
			}
			finally
			{
				Object.DestroyImmediate(instance);
			}
		}
	}
}
