using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rigging.Serialization
{
    public static class RigJson
    {
        private static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32
        };

        public static RigDefinition Read(string json)
        {
            if (json == null || json.Length > 4000000)
            {
                throw new ArgumentException("Invalid rig JSON size");
            }
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string field in new[] { "Format", "Version", "Id", "Joints", "Limbs" })
            {
                if (root[field] == null)
                {
                    throw new ArgumentException("Missing rig field: " + field);
                }
            }
            RigDefinition rig = JsonConvert.DeserializeObject<RigDefinition>(json, SETTINGS);
            rig.Validate();
            return rig;
        }

        public static string Write(RigDefinition rig)
        {
            rig.Validate();
            return JsonConvert.SerializeObject(rig, Formatting.Indented, SETTINGS);
        }
    }
}
