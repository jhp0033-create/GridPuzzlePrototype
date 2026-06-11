using UnityEngine;
using System.Collections.Generic;
using GridPuzzle.Data;

namespace GridPuzzle.Data
{
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "GridPuzzle/Level Data")]
    public class LevelData : ScriptableObject
    {
        public string levelName;
        public Vector2Int gridSize = new Vector2Int(14, 16);
        public Color[] palette;
        
        [Tooltip("List of all voxels in the level with their logical positions and initial states.")]
        public List<VoxelData> voxels = new List<VoxelData>();

        [System.Serializable]
        public class VehicleLaneData
        {
            public List<VehicleData> vehicles = new List<VehicleData>();
        }

        [Tooltip("3 separate lanes of vehicles for the selection queue.")]
        public VehicleLaneData[] selectionLanes = new VehicleLaneData[3];

        // Removes all voxel entries from the current level asset.
        public void Clear()
        {
            voxels.Clear();
        }

        // Adds a voxel entry to the serialized level definition.
        public void AddVoxel(VoxelData voxel)
        {
            voxels.Add(voxel);
        }
    }
}
