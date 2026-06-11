using UnityEngine;
using GridPuzzle.Data;

namespace GridPuzzle.Managers
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Configuration")]
        [SerializeField] private LevelData level1Data;
        
        // Caches the singleton entry point for level loading.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Loads the single-assignment level and runs validation before spawning content.
        public void LoadLevel1()
        {
            if (level1Data == null)
            {
                level1Data = Resources.Load<LevelData>("Levels/Level_1");
            }

            if (level1Data != null)
            {
              

                VoxelGridManager.Instance.InitializeGrid(level1Data);
                VehicleManager.Instance.InitializeVehicles(level1Data);
            }
        }

        // Exposes the currently configured level asset to other systems.
        public LevelData GetLevel1() => level1Data;
    }
}
