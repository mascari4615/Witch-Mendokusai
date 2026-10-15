using System;
using System.Numerics;
using NUnit.Framework;
using Rigging.Unity;

namespace Rigging.Tests
{
    public sealed class RiggingTests
    {
        [Test]
        public void SolverPreservesLengthsAndUsesRequestedBendSide()
        {
            LimbSolution solution = TwoBoneIk.Solve(Pose(), new Vector3(1, 1, 0), Vector3.UnitZ);
            Assert.That(Vector3.Distance(Vector3.Zero, solution.Elbow), Is.EqualTo(1).Within(.00001));
            Assert.That(Vector3.Distance(solution.Elbow, solution.End), Is.EqualTo(1).Within(.00001));
            Assert.That(Vector3.Distance(solution.End, new Vector3(1, 1, 0)), Is.LessThan(.00001));
            Assert.That(solution.Elbow.Z, Is.GreaterThan(0));
            Assert.That(solution.Reachable, Is.True);
        }

        [Test]
        public void UnreachableAndCoincidentGoalsRemainFinite()
        {
            LimbSolution far = TwoBoneIk.Solve(Pose(), new Vector3(-20, 0, 0), Vector3.UnitY);
            Assert.That(far.End.X, Is.EqualTo(-2).Within(.00001));
            Assert.That(far.Reachable, Is.False);
            LimbSolution coincident = TwoBoneIk.Solve(Pose(), Vector3.Zero, Vector3.UnitX);
            Assert.That(float.IsNaN(coincident.UpperRotation.W), Is.False);
            Assert.That(Vector3.Distance(coincident.Elbow, coincident.End), Is.EqualTo(1).Within(.00001));
            Assert.Throws<ArgumentException>(() => TwoBoneIk.Solve(Pose(), new Vector3(float.NaN, 0, 0), Vector3.UnitY));
            LimbPose zero = Pose();
            zero.Lower = zero.Upper;
            Assert.Throws<ArgumentException>(() => TwoBoneIk.Solve(zero, Vector3.One, Vector3.UnitY));
        }

        [Test]
        public void RandomReachableGoalsMatchForwardRotationEvaluation()
        {
            Random random = new Random(4615);
            for (int iteration = 0; iteration < 500; iteration++)
            {
                Vector3 goal = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * 2;
                LimbSolution solution = TwoBoneIk.Solve(Pose(), goal, new Vector3(.3f, 1, .7f));
                Vector3 elbow = Vector3.Transform(Vector3.UnitX, solution.UpperRotation);
                Vector3 end = elbow + Vector3.Transform(Vector3.UnitX, solution.LowerRotation);
                Assert.That(Vector3.Distance(end, goal), Is.LessThan(.00002), "goal " + iteration);
                Assert.That(Vector3.Distance(elbow, solution.Elbow), Is.LessThan(.00002));
            }
        }

        [Test]
        public void UnityAdapterSolvesTranslatedRotatedHierarchyAndRollsBackUnsupportedScale()
        {
            UnityEngine.GameObject parent = new UnityEngine.GameObject("rig-parent");
            try
            {
                parent.transform.position = new UnityEngine.Vector3(3, 2, 1);
                parent.transform.rotation = UnityEngine.Quaternion.Euler(20, 30, 40);
                UnityEngine.Transform upper = new UnityEngine.GameObject("upper").transform;
                UnityEngine.Transform lower = new UnityEngine.GameObject("lower").transform;
                UnityEngine.Transform end = new UnityEngine.GameObject("end").transform;
                upper.SetParent(parent.transform, false);
                lower.SetParent(upper, false);
                end.SetParent(lower, false);
                lower.localPosition = UnityEngine.Vector3.right;
                end.localPosition = UnityEngine.Vector3.right;
                UnityEngine.Vector3 goal = parent.transform.TransformPoint(new UnityEngine.Vector3(1, 1, 0));
                TransformLimbIk.Apply(upper, lower, end, goal, parent.transform.TransformPoint(UnityEngine.Vector3.forward));
                Assert.That(UnityEngine.Vector3.Distance(end.position, goal), Is.LessThan(.00002));
                UnityEngine.Quaternion original = upper.localRotation;
                parent.transform.localScale = new UnityEngine.Vector3(1, 2, 1);
                Assert.Throws<ArgumentException>(() => TransformLimbIk.Apply(upper, lower, end, goal, UnityEngine.Vector3.up));
                Assert.That(UnityEngine.Quaternion.Angle(original, upper.localRotation), Is.LessThan(.001));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RigDefinitionRejectsAmbiguousRolesAndMissingLimbJoints()
        {
            RigDefinition rig = new RigDefinition
            {
                Id = "test", Joints = new[] { new RigJoint { Role = "upper", Node = "a" }, new RigJoint { Role = "lower", Node = "b" }, new RigJoint { Role = "end", Node = "c" } },
                Limbs = new[] { new RigLimb { Id = "limb", Upper = "upper", Lower = "lower", End = "end" } }
            };
            rig.Validate();
            Assert.That(rig.Resolve("end"), Is.EqualTo("c"));
            rig.Joints[1].Role = "upper";
            Assert.Throws<ArgumentException>(() => rig.Validate());
        }

        private static LimbPose Pose()
        {
            return new LimbPose { Upper = Vector3.Zero, Lower = Vector3.UnitX, End = Vector3.UnitX * 2, UpperRotation = Quaternion.Identity, LowerRotation = Quaternion.Identity };
        }
    }
}
