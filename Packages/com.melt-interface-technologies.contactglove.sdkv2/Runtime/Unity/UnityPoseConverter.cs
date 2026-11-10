using UnityEngine;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Unity
{
    public static class UnityPoseConverter
    {
        // B=diag(1,1,-1), determinant -1. Position Bp, rotation BRB^-1.
        public static Vector3 Position(WireVector3 value) => new Vector3(value.X, value.Y, -value.Z);
        public static Quaternion Rotation(WireQuaternion value) => new Quaternion(-value.X, -value.Y, value.Z, value.W);
        public static void ModelRotations(HandSnapshot snapshot, Quaternion[] destination)
        {
            if (destination == null || destination.Length < 25) throw new System.ArgumentException("25 rotations required.");
            destination[0] = Quaternion.identity; // A mathematical root basis, never a measured wrist orientation.
            int rotation = 0;
            for (int node = 1; node < 25; node++)
            {
                int parent = HandTopology.Parents[node];
                bool present = rotation < 19 && HandTopology.RotationNodes[rotation] == node;
                destination[node] = destination[parent] * (present ? Rotation(snapshot.Rotations[rotation++]) : Quaternion.identity);
            }
        }
    }
}
