using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 적중 효과 하나. 상태이상 이름은 UnitStatusType 이름만 (숫자 금지)
	internal sealed class SkillMaterialEffect
	{
		public UnitStatusType Status;
		public float Seconds;
		public float SlowFactor;
	}

	// 이펙트 겹 하나. 종류마다 쓰는 칸이 다름
	// - Burst: Radius, Seconds, RingSeconds (그린 터짐)
	// - CombatFx: Asset (CombatFx 에셋 경로), Radius, Length, Width, Seconds, Follow, TurnsPerSecond (기존 전투 연출 재생)
	// OnBlast 면 탄이 아니라 탄이 내는 일격 (터짐 프리팹) 에 붙음. 일격을 내는 모양 (강화 일격, 지연 일격) 만
	internal sealed class SkillMaterialLayer
	{
		public string Kind;
		public float Radius;
		public float Seconds;
		public float RingSeconds;
		public string Asset;
		public float Length;
		public float Width;
		public bool Follow;
		public float TurnsPerSecond;
		public bool OnBlast;
	}

	// 레시피 하나를 공통 기본값, 모양 기본값과 합친 결과. 수치는 이름으로 꺼내고 없으면 바로 실패
	internal sealed class SkillMaterialSpec
	{
		public string Id;
		public string Form;
		public string Element;
		public Color Color;
		// 속성의 그린 텍스처 벌 (PaintedFxSet 경로). 지금은 전부 흰색으로 구운 PFX_White, 색은 정점색 (속성색)
		public string ElementArt;
		public float BodyWhiten;
		public List<SkillMaterialEffect> Effects = new();
		public List<SkillMaterialLayer> Layers = new();
		public JObject Values;

		public float Float(string key) => Value(key).Value<float>();
		public int Int(string key) => Value(key).Value<int>();
		public string Text(string key) => Value(key).Value<string>();

		private JToken Value(string key) => Values.TryGetValue(key, out JToken token) ? token : throw new InvalidOperationException($"{Id}: 값 없음 {key}");
	}

	// 재료 목록 읽기와 검증. 정본 skill-materials.json (이 폴더)
	// - 덮어쓰는 순서: Defaults -> Forms[모양] -> 레시피. 레시피는 위 둘에 있는 키만 쓸 수 있음 (오타는 바로 실패)
	// - 전부 검증한 뒤에야 굽기 시작 (반쯤 구운 상태로 멈추지 않게)
	internal static class SkillMaterialCatalog
	{
		private const string DATA_PATH = "Assets/_WitchMendokusai/Editor/SkillMaterials/skill-materials.json";
		private static readonly Regex ID_PATTERN = new("^[A-Za-z][A-Za-z0-9_]*$");
		private static readonly HashSet<string> RECIPE_ONLY_KEYS = new() { "Id", "Form", "Element", "Effects", "Layers" };
		private static readonly HashSet<string> EFFECT_KEYS = new() { "Status", "Seconds", "SlowFactor" };
		private static readonly Dictionary<string, HashSet<string>> LAYER_KEYS = new()
		{
			["Burst"] = new HashSet<string> { "Kind", "Radius", "Seconds", "RingSeconds", "OnBlast" },
			["CombatFx"] = new HashSet<string> { "Kind", "Asset", "Radius", "Length", "Width", "Seconds", "Follow", "TurnsPerSecond", "OnBlast" },
		};

		// 레시피 묶음 파일 (skill-materials-set*.json, 같은 폴더) 의 Recipes 를 정본 파일 뒤에 이어 붙임. 묶음은 생성기가 씀
		private const string SET_FOLDER = "Assets/_WitchMendokusai/Editor/SkillMaterials";
		private const string SET_PATTERN = "skill-materials-set*.json";

		public static List<SkillMaterialSpec> Load() => Parse(LoadText());

		public static string LoadText()
		{
			TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(DATA_PATH);
			if (text == null)
				throw new InvalidOperationException($"재료 목록 없음: {DATA_PATH}");
			JObject root = JObject.Parse(text.text);
			JArray recipes = Required<JArray>(root, "Recipes", "파일");
			string[] sets = System.IO.Directory.GetFiles(SET_FOLDER, SET_PATTERN);
			Array.Sort(sets, StringComparer.Ordinal);
			foreach (string setPath in sets)
			{
				JObject set = JObject.Parse(System.IO.File.ReadAllText(setPath));
				foreach (JToken recipe in Required<JArray>(set, "Recipes", setPath))
					recipes.Add(recipe);
			}
			return root.ToString();
		}

		public static List<SkillMaterialSpec> Parse(string json)
		{
			JObject root = JObject.Parse(json);
			JObject defaults = Required<JObject>(root, "Defaults", "파일");
			JObject forms = Required<JObject>(root, "Forms", "파일");
			JObject elements = Required<JObject>(root, "Elements", "파일");
			float bodyWhiten = Required<JObject>(root, "Look", "파일").Value<float>("BodyWhiten");

			List<SkillMaterialSpec> specs = new();
			HashSet<string> ids = new();
			foreach (JObject recipe in Required<JArray>(root, "Recipes", "파일").Cast<JObject>())
			{
				string id = recipe.Value<string>("Id");
				if (id == null || ID_PATTERN.IsMatch(id) == false)
					throw new InvalidOperationException($"재료 이름이 비었거나 파일 이름에 못 씀: '{id}'");
				if (ids.Add(id) == false)
					throw new InvalidOperationException($"재료 이름 중복: {id}");

				string form = Required<JValue>(recipe, "Form", id).Value<string>();
				if (SkillMaterialForms.Has(form) == false)
					throw new InvalidOperationException($"{id}: 굽는 법 없는 모양 {form}");
				JObject formDefaults = Required<JObject>(forms, form, $"Forms ({id})");

				JObject values = (JObject)defaults.DeepClone();
				values.Merge(formDefaults);
				foreach (JProperty property in recipe.Properties())
				{
					if (RECIPE_ONLY_KEYS.Contains(property.Name))
						continue;
					if (values.ContainsKey(property.Name) == false)
						throw new InvalidOperationException($"{id}: 모르는 키 {property.Name} (Defaults 나 Forms.{form} 에 없음)");
					values[property.Name] = property.Value;
				}

				string element = Required<JValue>(recipe, "Element", id).Value<string>();
				JObject color = Required<JObject>(elements, element, $"Elements ({id})");
				// 겹은 레시피에 있으면 그것, 없으면 모양 기본값 (통째로 바꿈, 섞지 않음)
				JToken layers = recipe.TryGetValue("Layers", out JToken recipeLayers) ? recipeLayers : formDefaults.GetValue("Layers");
				values.Remove("Layers");
				if (layers != null && layers.HasValues && SkillMaterialForms.Get(form).AcceptsLayers == false)
					throw new InvalidOperationException($"{id}: {form} 은 이펙트 겹을 받지 않음");
				// 일격 겹 (OnBlast) 은 일격을 내는 모양만. 다른 모양에서는 붙을 곳이 없어 조용히 빠지므로 바로 실패
				if (layers != null && layers.Any(layer => layer.Value<bool?>("OnBlast") == true) && SkillMaterialForms.Get(form) is SkillMaterialStrikeForm == false)
					throw new InvalidOperationException($"{id}: {form} 은 일격 겹 (OnBlast) 을 받지 않음");
				specs.Add(new SkillMaterialSpec
				{
					Id = id,
					Form = form,
					Element = element,
					Color = new Color(color.Value<float>("r"), color.Value<float>("g"), color.Value<float>("b"), 1f),
					ElementArt = Required<JValue>(color, "Art", $"Elements.{element}").Value<string>(),
					BodyWhiten = bodyWhiten,
					Effects = ParseEffects(recipe, id),
					Layers = ParseLayers(layers, id, values),
					Values = values,
				});
			}
			return specs;
		}

		private static List<SkillMaterialEffect> ParseEffects(JObject recipe, string id)
		{
			List<SkillMaterialEffect> effects = new();
			if (recipe.TryGetValue("Effects", out JToken token) == false)
				return effects;
			if (token is JArray == false || token.Any(item => item is JObject == false))
				throw new InvalidOperationException($"{id}: Effects 는 객체 배열이어야 함");
			foreach (JObject effect in token.Cast<JObject>())
			{
				foreach (JProperty property in effect.Properties())
				{
					if (EFFECT_KEYS.Contains(property.Name) == false)
						throw new InvalidOperationException($"{id}: 효과의 모르는 키 {property.Name}");
				}
				string name = Required<JValue>(effect, "Status", id).Value<string>();
				if (int.TryParse(name, out _) || Enum.IsDefined(typeof(UnitStatusType), name) == false)
					throw new InvalidOperationException($"{id}: 없는 상태이상 {name}");
				UnitStatusType status = (UnitStatusType)Enum.Parse(typeof(UnitStatusType), name);
				effects.Add(new SkillMaterialEffect
				{
					Status = status,
					Seconds = Required<JValue>(effect, "Seconds", id).Value<float>(),
					SlowFactor = status == UnitStatusType.Slow ? Required<JValue>(effect, "SlowFactor", id).Value<float>() : 0f,
				});
			}
			return effects;
		}

		// 겹 반지름을 빼면 모양 반지름 (Radius) 을 따름. 판정 범위와 그림 범위가 따로 놀지 않게
		private static List<SkillMaterialLayer> ParseLayers(JToken token, string id, JObject values)
		{
			List<SkillMaterialLayer> layers = new();
			if (token == null)
				return layers;
			if (token is JArray == false || token.Any(item => item is JObject == false))
				throw new InvalidOperationException($"{id}: Layers 는 객체 배열이어야 함");
			foreach (JObject layer in token.Cast<JObject>())
			{
				string kind = Required<JValue>(layer, "Kind", id).Value<string>();
				if (LAYER_KEYS.TryGetValue(kind, out HashSet<string> keys) == false)
					throw new InvalidOperationException($"{id}: 모르는 겹 {kind}");
				foreach (JProperty property in layer.Properties())
				{
					if (keys.Contains(property.Name) == false)
						throw new InvalidOperationException($"{id}: 겹 {kind} 의 모르는 키 {property.Name}");
				}
				bool isBurst = kind == "Burst";
				float radius = layer.TryGetValue("Radius", out JToken radiusToken) ? radiusToken.Value<float>() : Required<JValue>(values, "Radius", $"{id} 겹 반지름 (모양에 Radius 없음)").Value<float>();
				layers.Add(new SkillMaterialLayer
				{
					Kind = kind,
					Radius = radius,
					Seconds = Required<JValue>(layer, "Seconds", id).Value<float>(),
					RingSeconds = isBurst ? Required<JValue>(layer, "RingSeconds", id).Value<float>() : 0f,
					Asset = isBurst ? null : Required<JValue>(layer, "Asset", id).Value<string>(),
					Length = layer.TryGetValue("Length", out JToken length) ? length.Value<float>() : radius,
					Width = layer.TryGetValue("Width", out JToken width) ? width.Value<float>() : 1f,
					Follow = layer.Value<bool?>("Follow") ?? false,
					TurnsPerSecond = layer.Value<float?>("TurnsPerSecond") ?? 0f,
					OnBlast = layer.Value<bool?>("OnBlast") ?? false,
				});
			}
			return layers;
		}

		private static T Required<T>(JObject parent, string key, string where) where T : JToken
		{
			if (parent.TryGetValue(key, out JToken token) && token is T typed)
				return typed;
			throw new InvalidOperationException($"{where}: {key} 없음 또는 형식 다름");
		}
	}
}
