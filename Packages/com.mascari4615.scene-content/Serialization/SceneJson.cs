using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SceneContent.Serialization
{
    public static class SceneJson
    {
        public static string Write(SceneDocument document)
        {
            SceneValidation.Ensure(document);
            return JsonConvert.SerializeObject(document, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });
        }

        public static SceneDocument Read(string json)
        {
            if (json == null || json.Length > 32000000) throw new ArgumentException("Invalid scene JSON size");
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string field in new[] { "Format", "Version", "Id", "CoordinateFrame", "Nodes", "Meshes", "Materials", "Resources" })
                if (root[field] == null) throw new ArgumentException("Missing scene field: " + field);
            SceneDocument document = JsonConvert.DeserializeObject<SceneDocument>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32
            });
            SceneValidation.Ensure(document);
            return document;
        }
    }
}
