using UnityEngine;

namespace GridPuzzle.Data
{
    [System.Serializable]
    public class GameConfigData
    {
        [Header("Vehicle Settings")]
        public float vehicleRailSpeed = 8.0f;
        public int maxVehiclesOnRail = 5;
        public int maxStorageSlots = 5;
        public float laneVerticalSpacing = 2.0f;
        public float rotationLerpSpeed = 25.0f;
        public float deploymentMoveDuration = 0.25f;
        public float deploymentRotateDuration = 0.2f;

        [Header("Voxel Animation Settings")]
        public float voxelPopScale = 1.5f;
        public float voxelAbsorbDuration = 0.4f;
        public float wiggleMinAngle = 3.0f;
        public float wiggleMaxAngle = 6.0f;
        public float wiggleMinDuration = 0.4f;
        public float wiggleMaxDuration = 0.6f;
        public float wiggleMaxDelay = 0.5f;
        
        [Header("Game Pace")]
        public float endgameSpeedThresholdCount = 5;
        public float endgameTimeScale = 1.5f;

        [Header("Audio Settings")]
        public float absorbSoundCooldown = 0.05f;

        // Serializes the current config into a readable JSON snapshot.
        public string ToJson() => JsonUtility.ToJson(this, true);
        // Restores config values from a JSON string loaded at runtime.
        public static GameConfigData FromJson(string json) => JsonUtility.FromJson<GameConfigData>(json);
    }
}
