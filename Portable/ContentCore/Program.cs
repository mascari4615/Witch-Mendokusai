using System;
using System.Numerics;
using Motion;
using Rigging;
using SceneContent;

PoseClip clip = new PoseClip { Id = "portable-example", Duration = 2, Tracks = new PoseTrack[0] };
clip = PoseClipEditing.Record(clip, "joint", 0, new PoseValue(Vector3.Zero, Quaternion.Identity, Vector3.One));
clip = PoseClipEditing.Record(clip, "joint", 2, new PoseValue(Vector3.UnitX * 2, Quaternion.Identity, Vector3.One));
clip = PoseClipEditing.RecordValue(clip, "property", 0, 30);
clip = PoseClipEditing.RecordValue(clip, "property", 2, 60);
Model model = new Model();
float property = 30;
FloatPropertyTarget values = new FloatPropertyTarget();
values.Bind("property", () => property, value => property = value, 1, 179);
new PoseClipSampler(clip).Apply(1, model, values);
if (Math.Abs(model.Pose.Position.X - 1) > .00001f || property != 45) throw new Exception("Motion sampling failed");

clip = PoseClipEditing.RecordValue(clip, "property", 2, 500);
int writes = model.Writes;
try
{
    new PoseClipSampler(clip).Apply(2, model, values);
    throw new Exception("Invalid property accepted");
}
catch (ArgumentException)
{
    if (model.Writes != writes || property != 45) throw new Exception("Invalid frame was partially applied");
}

LimbSolution limb = TwoBoneIk.Solve(new LimbPose
{
    Upper = Vector3.Zero, Lower = Vector3.UnitX, End = Vector3.UnitX * 2,
    UpperRotation = Quaternion.Identity, LowerRotation = Quaternion.Identity
}, new Vector3(1, 1, 0), Vector3.UnitZ);
Vector3 evaluated = Vector3.Transform(Vector3.UnitX, limb.UpperRotation) + Vector3.Transform(Vector3.UnitX, limb.LowerRotation);
if (Vector3.Distance(evaluated, new Vector3(1, 1, 0)) > .00001f) throw new Exception("Limb forward evaluation failed");

SceneDocument scene = new SceneDocument
{
    Id = "portable-scene", Nodes = new[] { new SceneNode { Id = "root", Position = new float[3], Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } } },
    Meshes = new MeshInstance[0], Materials = new SurfaceMaterial[0], Resources = new ContentResource[0]
};
SceneDocument combined = SceneComposition.Append(scene, scene, "second", new[] { 2f, 0f, 0f });
if (combined.Nodes.Length != 3 || scene.Nodes.Length != 1) throw new Exception("Scene composition modified its source");
foreach (System.Reflection.AssemblyName reference in typeof(PoseClipSampler).Assembly.GetReferencedAssemblies())
    if (reference.Name.StartsWith("Unity", StringComparison.Ordinal)) throw new Exception("Engine dependency in portable core");
Console.WriteLine("PASS: five common cores compile without Unity; pose/property motion, whole-frame rejection, limb solving and scene composition execute through host ports.");

internal sealed class Model : IPoseTarget
{
    public PoseValue Pose;
    public int Writes;
    public PoseValue Read(string id) { return Pose; }
    public void Write(string id, PoseValue value) { Pose = value; Writes++; }
}
