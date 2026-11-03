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
		public void FromHitbox_ReachFollowsHitboxOutline()
		{
			GameObject root = new("Hitbox");
			try
			{
				BoxCollider box = root.AddComponent<BoxCollider>();
				box.center = new Vector3(0, 0, 2);
				box.size = new Vector3(1, 1, 2);

				SwordSwingArea area = SwordSwingArea.FromHitbox(root.transform);

				// 정면과 비낀 방향은 앞면까지, 옆으로 꺾인 방향은 옆면에서 끝남
				// 모서리는 측정점 사이에 떨어지면 이웃 측정점을 잇는 선분만큼 깎임, 33지점에서 이 상자는 약 0.11m
				Assert.That(area.ReachAt(0f), Is.EqualTo(3f).Within(0.001f));
				Assert.That(area.ReachAt(5f), Is.EqualTo(3f / Mathf.Cos(5f * Mathf.Deg2Rad)).Within(0.001f));
				float cornerAngle = Mathf.Atan2(0.5f, 3f) * Mathf.Rad2Deg;
				Assert.That(area.ReachAt(cornerAngle), Is.EqualTo(new Vector2(0.5f, 3f).magnitude).Within(0.15f));
				Assert.That(area.ReachAt(-20f), Is.EqualTo(0.5f / Mathf.Sin(20f * Mathf.Deg2Rad)).Within(0.001f));
				Assert.That(area.ReachAt(area.HalfAngle + 5f), Is.EqualTo(0f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void CopyReachTo_KeepsProfileWhenSourceBufferChanges()
		{
			GameObject root = new("Hitbox");
			try
			{
				BoxCollider box = root.AddComponent<BoxCollider>();
				box.center = new Vector3(0, 0, 2);
				box.size = new Vector3(1, 1, 2);
				float[] shared = new float[SwordSwingArea.REACH_SAMPLES];
				float[] own = new float[SwordSwingArea.REACH_SAMPLES];
				SwordSwingArea copy = SwordSwingArea.FromHitbox(root.transform, shared).CopyReachTo(own);

				box.size = new Vector3(1, 1, 6);
				SwordSwingArea.FromHitbox(root.transform, shared);

				Assert.That(copy.ReachAt(0f), Is.EqualTo(3f).Within(0.01f));
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
