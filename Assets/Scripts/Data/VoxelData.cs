using UnityEngine;

namespace GridPuzzle.Data
{
    [System.Serializable]
    public class VoxelData
    {
        public string uniqueID;
        public ColorType colorType;
        public Vector2Int gridPosition;
        public ExposeDirection exposedFaces;
        public bool isAbsorbed;
        public bool isClaimed;

        // Creates one logical voxel entry at a fixed grid position.
        public VoxelData(string id, ColorType color, Vector2Int pos)
        {
            uniqueID = id;
            colorType = color;
            gridPosition = pos;
            exposedFaces = ExposeDirection.None;
            isAbsorbed = false;
            isClaimed = false;
        }

        // Copies voxel state so runtime mutation never pollutes the source asset.
        public VoxelData Clone()
        {
            return new VoxelData(uniqueID, colorType, gridPosition)
            {
                exposedFaces = this.exposedFaces,
                isAbsorbed = this.isAbsorbed,
                isClaimed = this.isClaimed
            };
        }
    }
}
