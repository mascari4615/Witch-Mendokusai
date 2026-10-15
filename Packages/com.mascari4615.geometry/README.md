# Geometry

Engine independent positions, normals, tangent frames, vertex colors, UVs, triangle groups, four-influence linear skinning and sparse multi-frame morph targets. No game, renderer or editor types in Core or Binary. Unity conversion lives in its own assembly.

Binary version 2 (`NGM2`) uses little endian buffers with explicit lengths and bounded allocation. Morph frames contain ascending sparse vertex indices and position/normal/tangent deltas. Frame weights use normalized units: `1` represents the full target. Unknown versions, invalid references, non-finite values, truncated buffers and trailing data are rejected.

Coordinates are right handed, Y up, with negative Z forward. Matrices use column-major storage. Triangle front faces are counterclockwise. The Unity adapter reflects Z, reverses winding and tangent handedness, and applies the same basis change to bind matrices. UV origin is bottom left.

The codec does not carry source asset IDs, scripts, components or extension payloads. Geometry resources can be shared by multiple scene instances and identified by content hash.
