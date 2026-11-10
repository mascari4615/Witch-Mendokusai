using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public sealed class PlayerBodyTintTests
	{
		[TestCase(true)]
		[TestCase(false)]
		public void TintRetainsEachMaterialColorAndUnrelatedPropertiesOnReuse(bool neutralTint)
		{
			GameObject host = new GameObject("BodyTintTest");
			host.SetActive(false);
			Doll doll = ScriptableObject.CreateInstance<Doll>();
			Material cyan = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			Material white = new Material(cyan);
			Color original = new Color(.35f, .63f, .76f, 1f);
			Color tint = neutralTint ? Color.white : new Color(.6f, .8f, .9f, 1f);
			try
			{
				PlayerObject player = host.AddComponent<PlayerObject>();
				GameObject mesh = new GameObject("TwoMaterialHat");
				mesh.transform.SetParent(host.transform);
				MeshRenderer renderer = mesh.AddComponent<MeshRenderer>();
				cyan.SetColor("_BaseColor", original);
				white.SetColor("_BaseColor", Color.white);
				renderer.sharedMaterials = new[] { cyan, white };
				SerializedObject playerFields = new SerializedObject(player);
				playerFields.FindProperty("<MeshParent>k__BackingField").objectReferenceValue = mesh.transform;
				playerFields.FindProperty("<UnitData>k__BackingField").objectReferenceValue = doll;
				playerFields.ApplyModifiedPropertiesWithoutUndo();
				SerializedObject dollFields = new SerializedObject(doll);
				dollFields.FindProperty("<BodyTint>k__BackingField").colorValue = tint;
				dollFields.ApplyModifiedPropertiesWithoutUndo();
				MaterialPropertyBlock properties = new MaterialPropertyBlock();
				properties.SetFloat("_Smoothness", .23f);
				renderer.SetPropertyBlock(properties, 0);
				MethodInfo apply = typeof(PlayerObject).GetMethod("ApplyBodyTint", BindingFlags.Instance | BindingFlags.NonPublic);
				for (int reuse = 0; reuse < 2; reuse++)
				{
					apply.Invoke(player, null);
					renderer.GetPropertyBlock(properties, 0);
					Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"), original * tint), Is.LessThan(.00001f));
					Assert.That(properties.GetFloat("_Smoothness"), Is.EqualTo(.23f));
					renderer.GetPropertyBlock(properties, 1);
					Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"), tint), Is.LessThan(.00001f));
				}
				Assert.That(Vector4.Distance(cyan.GetColor("_BaseColor"), original), Is.LessThan(.00001f));
			}
			finally
			{
				Object.DestroyImmediate(host);
				Object.DestroyImmediate(doll);
				Object.DestroyImmediate(cyan);
				Object.DestroyImmediate(white);
			}
		}
	}
}
