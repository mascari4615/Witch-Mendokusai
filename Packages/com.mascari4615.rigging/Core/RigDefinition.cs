using System;
using System.Collections.Generic;

namespace Rigging
{
    [Serializable]
    public sealed class RigDefinition
    {
        public string Format = "rig-definition";
        public int Version = 1;
        public string Id;
        public RigJoint[] Joints;
        public RigLimb[] Limbs;

        public void Validate()
        {
            if (Format != "rig-definition" || Version != 1 || string.IsNullOrEmpty(Id) || Joints == null || Limbs == null ||
                Joints.Length > 10000 || Limbs.Length > 1000)
            {
                throw new ArgumentException("Invalid rig definition header");
            }
            HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigJoint joint in Joints)
            {
                if (joint == null || string.IsNullOrEmpty(joint.Role) || string.IsNullOrEmpty(joint.Node) || roles.Add(joint.Role) == false)
                {
                    throw new ArgumentException("Rig requires unique named roles and node bindings");
                }
            }
            HashSet<string> limbs = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigLimb limb in Limbs)
            {
                if (limb == null || string.IsNullOrEmpty(limb.Id) || limbs.Add(limb.Id) == false ||
                    roles.Contains(limb.Upper) == false || roles.Contains(limb.Lower) == false || roles.Contains(limb.End) == false ||
                    limb.Upper == limb.Lower || limb.Lower == limb.End || limb.Upper == limb.End)
                {
                    throw new ArgumentException("Rig limbs must reference three distinct existing roles");
                }
            }
        }

        public string Resolve(string role)
        {
            foreach (RigJoint joint in Joints)
            {
                if (joint.Role == role)
                {
                    return joint.Node;
                }
            }
            throw new KeyNotFoundException("Missing rig role: " + role);
        }
    }

    [Serializable]
    public sealed class RigJoint
    {
        public string Role;
        public string Node;
    }

    [Serializable]
    public sealed class RigLimb
    {
        public string Id;
        public string Upper;
        public string Lower;
        public string End;
    }
}
