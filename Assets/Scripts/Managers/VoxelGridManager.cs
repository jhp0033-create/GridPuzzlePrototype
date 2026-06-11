using UnityEngine;
using System.Collections.Generic;
using GridPuzzle.Data;
using GridPuzzle.Core;

namespace GridPuzzle.Managers
{
    public class VoxelGridManager : MonoBehaviour
    {
        public static VoxelGridManager Instance { get; private set; }

        [Header("Grid Settings")]
        [SerializeField] private GameObject voxelPrefab; // Keep for inspector reference if needed
        [SerializeField] private Transform gridParent;
        [SerializeField] private Vector2 voxelSpacing = new Vector2(1.1f, 1.1f);
        [SerializeField] private Vector3 gridOffset = Vector3.zero;
        [SerializeField] private float absorptionDistance = 1.5f;

        public float AbsorptionDistance => absorptionDistance;

        private Dictionary<Vector2Int, VoxelModel> voxelGrid = new Dictionary<Vector2Int, VoxelModel>();
        private Dictionary<ExposeDirection, List<VoxelModel>> exposedVoxels = new Dictionary<ExposeDirection, List<VoxelModel>>();
        private List<VoxelModel> claimableBuffer = new List<VoxelModel>();
        private Color[] currentPalette;

        // Keeps spawned voxels aligned when spacing values change in the inspector.
        private void OnValidate()
        {
            if (voxelGrid != null && voxelGrid.Count > 0)
            {
                UpdateGridPositions();
            }
        }

        // Initializes the grid manager singleton and assigns a fallback parent.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (gridParent == null) gridParent = this.transform;
        }

        private Vector2Int currentGridSize = new Vector2Int(14, 16);

        // Rebuilds the runtime voxel grid from the serialized level asset.
        public void InitializeGrid(LevelData levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("LevelData is null! Cannot initialize grid.");
                return;
            }

            currentGridSize = levelData.gridSize;
            currentPalette = levelData.palette;
            ClearGrid();

            foreach (var originalData in levelData.voxels)
            {
                // Sanitize and protect the ScriptableObject from runtime pollution
                VoxelData data = originalData.Clone();

                // Hard-reset flags that might have been polluted in previous play sessions
                data.isClaimed = false;
                data.isAbsorbed = false;

                // Strictly calculate exposure based ONLY on physical boundaries (14x16)
                data.exposedFaces = ExposeDirection.None;
                if (data.gridPosition.y == 15) data.exposedFaces |= ExposeDirection.Top;
                if (data.gridPosition.x == 13) data.exposedFaces |= ExposeDirection.Right;
                if (data.gridPosition.y == 0) data.exposedFaces |= ExposeDirection.Bottom;
                if (data.gridPosition.x == 0) data.exposedFaces |= ExposeDirection.Left;

                SpawnVoxel(data);
            }

            Debug.Log($"<color=cyan>[VoxelGridManager]</color> Grid Init Done. Total: {voxelGrid.Count} voxels.");
            
            // Detailed breakdown by direction and color
            foreach (ExposeDirection dir in System.Enum.GetValues(typeof(ExposeDirection)))
            {
                if (dir == ExposeDirection.None) continue;
                
                Dictionary<ColorType, int> colorStats = new Dictionary<ColorType, int>();
                // Initialize with 0 for all colors (Red, Blue, Green, Yellow, Purple)
                for (int i = 1; i <= 5; i++) colorStats[(ColorType)i] = 0;

                if (exposedVoxels.TryGetValue(dir, out var list))
                {
                    foreach (var voxel in list)
                    {
                        if (colorStats.ContainsKey(voxel.Data.colorType))
                            colorStats[voxel.Data.colorType]++;
                    }
                }

                string statsStr = "";
                foreach (var kvp in colorStats) 
                {
                    statsStr += $"Color_{(int)kvp.Key}:{kvp.Value} | ";
                }
                Debug.Log($"<color=orange>[Exposed Stats] Side {dir}:</color> {statsStr} | Total Count: {list.Count}");
            }
            
            Debug.Log($"<color=green>[Outer Shell Summary]</color> Top: {exposedVoxels[ExposeDirection.Top].Count}, Right: {exposedVoxels[ExposeDirection.Right].Count}, Bottom: {exposedVoxels[ExposeDirection.Bottom].Count}, Left: {exposedVoxels[ExposeDirection.Left].Count}");
        }

        // Inserts a voxel into a side list using the traversal order for that rail segment.
        private void InsertSorted(List<VoxelModel> list, VoxelModel voxel, ExposeDirection dir)
        {
            int index = 0;
            for (int i = 0; i < list.Count; i++)
            {
                bool insertHere = false;
                switch (dir)
                {
                    case ExposeDirection.Bottom: insertHere = voxel.Data.gridPosition.x < list[i].Data.gridPosition.x; break;
                    case ExposeDirection.Top:    insertHere = voxel.Data.gridPosition.x > list[i].Data.gridPosition.x; break;
                    case ExposeDirection.Right:  insertHere = voxel.Data.gridPosition.y < list[i].Data.gridPosition.y; break;
                    case ExposeDirection.Left:   insertHere = voxel.Data.gridPosition.y > list[i].Data.gridPosition.y; break;
                }
                if (insertHere)
                {
                    index = i;
                    break;
                }
                index = i + 1;
            }
            list.Insert(index, voxel);
        }

        // Spawns one runtime voxel, colors it, and registers its initial exposed sides.
        private void SpawnVoxel(VoxelData data)
        {
            Vector3 worldPos = CalculateWorldPosition(data.gridPosition);

            // Re-integrated VoxelPool as per user instruction
            VoxelModel model = VoxelPool.Instance.Get();
            if (model == null) return;

            model.transform.position = worldPos;
            model.transform.rotation = Quaternion.identity;
            model.transform.SetParent(gridParent);
            model.gameObject.name = $"Voxel_{data.gridPosition.x}_{data.gridPosition.y}";
            
            Color voxelColor = Color.white;
            if (currentPalette != null && (int)data.colorType > 0 && (int)data.colorType <= currentPalette.Length)
            {
                voxelColor = currentPalette[(int)data.colorType - 1];
            }

            model.Initialize(data, voxelColor);
            voxelGrid.Add(data.gridPosition, model);
            
            if (model.IsExposedFrom(ExposeDirection.Top)) InsertSorted(exposedVoxels[ExposeDirection.Top], model, ExposeDirection.Top);
            if (model.IsExposedFrom(ExposeDirection.Right)) InsertSorted(exposedVoxels[ExposeDirection.Right], model, ExposeDirection.Right);
            if (model.IsExposedFrom(ExposeDirection.Bottom)) InsertSorted(exposedVoxels[ExposeDirection.Bottom], model, ExposeDirection.Bottom);
            if (model.IsExposedFrom(ExposeDirection.Left)) InsertSorted(exposedVoxels[ExposeDirection.Left], model, ExposeDirection.Left);

            // Visually trigger 'Exposed' animation if it belongs to the initial outer shell
            if (model.Data.exposedFaces != ExposeDirection.None)
            {
                model.TriggerExposed();
            }
        }

        // Returns all live voxels to the pool and resets exposure caches.
        public void ClearGrid()
        {
            foreach (var voxel in voxelGrid.Values)
            {
                if (voxel != null)
                {
                    VoxelPool.Instance.ReturnToPool(voxel);
                }
            }
            voxelGrid.Clear();
            exposedVoxels.Clear();
            exposedVoxels[ExposeDirection.Top] = new List<VoxelModel>();
            exposedVoxels[ExposeDirection.Right] = new List<VoxelModel>();
            exposedVoxels[ExposeDirection.Bottom] = new List<VoxelModel>();
            exposedVoxels[ExposeDirection.Left] = new List<VoxelModel>();
        }

        // Converts logical grid coordinates into runtime world coordinates.
        private Vector3 CalculateWorldPosition(Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * voxelSpacing.x,
                0, 
                gridPos.y * voxelSpacing.y
            ) + gridOffset;
        }

        // Repositions all live voxels after inspector-driven spacing changes.
        private void UpdateGridPositions()
        {
            foreach (var kvp in voxelGrid)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.transform.position = CalculateWorldPosition(kvp.Key);
                }
            }
        }

        // Returns the runtime voxel at a logical grid position if it still exists.
        public VoxelModel GetVoxelAt(Vector2Int position)
        {
            voxelGrid.TryGetValue(position, out var voxel);
            return voxel;
        }

        // Reserves currently exposed voxels for one rail segment before movement begins.
        public List<VoxelModel> PreClaimVoxels(ColorType color, ExposeDirection requiredDirection, int maxQuota, Vector3 startPos)
        {
            claimableBuffer.Clear();

            if (exposedVoxels.TryGetValue(requiredDirection, out var list))
            {
                foreach(var voxel in list) 
                {
                    if (!voxel.Data.isClaimed && voxel.Data.colorType == color)
                    {
                        claimableBuffer.Add(voxel);
                    }
                }
            }
            else
            {
                Debug.LogError($"[PreClaim] CRITICAL: Direction {requiredDirection} not in exposedVoxels cache!");
            }

            // No need to sort claimableBuffer here. The exposedVoxels list is inherently sorted 
            // when initialized and maintained in sorted order via InsertSorted() during gameplay.

            List<VoxelModel> finalClaimed = new List<VoxelModel>();
            int count = Mathf.Min(maxQuota, claimableBuffer.Count);

            for (int i = 0; i < count; i++)
            {
                VoxelModel targetVoxel = claimableBuffer[i];
                
                // Atomically claim it, but DO NOT update neighbors yet to prevent ghost exposures!
                ClaimVoxelLogical(targetVoxel); 
                
                finalClaimed.Add(targetVoxel);
            }

            return finalClaimed;
        }

        // Applies the logical claim immediately so following vehicles see the updated shell.
        private void ClaimVoxelLogical(VoxelModel voxel)
        {
            voxel.Data.isClaimed = true; // Atomic claim upfront
            
            // Remove from all queues just to be safe
            if (exposedVoxels.ContainsKey(ExposeDirection.Top)) exposedVoxels[ExposeDirection.Top].Remove(voxel);
            if (exposedVoxels.ContainsKey(ExposeDirection.Right)) exposedVoxels[ExposeDirection.Right].Remove(voxel);
            if (exposedVoxels.ContainsKey(ExposeDirection.Bottom)) exposedVoxels[ExposeDirection.Bottom].Remove(voxel);
            if (exposedVoxels.ContainsKey(ExposeDirection.Left)) exposedVoxels[ExposeDirection.Left].Remove(voxel);

            // Logically update neighbors IMMEDIATELY so the next vehicle can pre-claim them at the waypoint!
            UpdateNeighborsAfterAbsorption(voxel.Data.gridPosition);
        }

        // Resolves the physical voxel removal after its flight animation completes.
        public void AbsorbVoxel(VoxelModel voxel)
        {
            Vector2Int pos = voxel.Data.gridPosition;
            if (voxelGrid.ContainsKey(pos))
            {
                // Physical removal and return to pool
                voxelGrid.Remove(pos);
                VoxelPool.Instance.ReturnToPool(voxel);

                // Visually trigger 'Exposed' animation on newly revealed physical neighbors.
                // Cast rays outward to find the first solid voxel in each direction, skipping gaps.
                
                // Right (+X)
                for (int x = pos.x + 1; x < currentGridSize.x; x++)
                {
                    if (voxelGrid.TryGetValue(new Vector2Int(x, pos.y), out var v)) { v.TriggerExposed(); break; }
                }
                // Left (-X)
                for (int x = pos.x - 1; x >= 0; x--)
                {
                    if (voxelGrid.TryGetValue(new Vector2Int(x, pos.y), out var v)) { v.TriggerExposed(); break; }
                }
                // Up (+Y)
                for (int y = pos.y + 1; y < currentGridSize.y; y++)
                {
                    if (voxelGrid.TryGetValue(new Vector2Int(pos.x, y), out var v)) { v.TriggerExposed(); break; }
                }
                // Down (-Y)
                for (int y = pos.y - 1; y >= 0; y--)
                {
                    if (voxelGrid.TryGetValue(new Vector2Int(pos.x, y), out var v)) { v.TriggerExposed(); break; }
                }

                // Check for Victory: All voxels cleared
                if (voxelGrid.Count == 0)
                {
                    GameManager.Instance.ChangeState(GameManager.GameState.Victory);
                }
            }
        }

        // Recomputes the newly exposed shell voxels for the affected row and column.
        private void UpdateNeighborsAfterAbsorption(Vector2Int absorbedPos)
        {
            // The absorbed voxel at (x, y) created empty space that could potentially expose new outermost voxels 
            // for the Top, Bottom, Left, and Right rails along that specific row and column.
            // We mathematically guarantee structural integrity by recalculating the outermost voxel 
            // for the affected column (X) and row (Y) using 4 targeted inward raycasts.

            // 1. Bottom Rail: Raycast UP from the bottom edge of the affected column
            for (int y = 0; y < currentGridSize.y; y++)
            {
                if (voxelGrid.TryGetValue(new Vector2Int(absorbedPos.x, y), out var voxel) && !voxel.Data.isClaimed)
                {
                    if (!voxel.IsExposedFrom(ExposeDirection.Bottom))
                    {
                        voxel.SetExposed(ExposeDirection.Bottom);
                        InsertSorted(exposedVoxels[ExposeDirection.Bottom], voxel, ExposeDirection.Bottom);
                    }
                    break; // Found the outermost voxel for this column from the bottom
                }
            }

            // 2. Top Rail: Raycast DOWN from the top edge of the affected column
            for (int y = currentGridSize.y - 1; y >= 0; y--)
            {
                if (voxelGrid.TryGetValue(new Vector2Int(absorbedPos.x, y), out var voxel) && !voxel.Data.isClaimed)
                {
                    if (!voxel.IsExposedFrom(ExposeDirection.Top))
                    {
                        voxel.SetExposed(ExposeDirection.Top);
                        InsertSorted(exposedVoxels[ExposeDirection.Top], voxel, ExposeDirection.Top);
                    }
                    break;
                }
            }

            // 3. Left Rail: Raycast RIGHT from the left edge of the affected row
            for (int x = 0; x < currentGridSize.x; x++)
            {
                if (voxelGrid.TryGetValue(new Vector2Int(x, absorbedPos.y), out var voxel) && !voxel.Data.isClaimed)
                {
                    if (!voxel.IsExposedFrom(ExposeDirection.Left))
                    {
                        voxel.SetExposed(ExposeDirection.Left);
                        InsertSorted(exposedVoxels[ExposeDirection.Left], voxel, ExposeDirection.Left);
                    }
                    break;
                }
            }

            // 4. Right Rail: Raycast LEFT from the right edge of the affected row
            for (int x = currentGridSize.x - 1; x >= 0; x--)
            {
                if (voxelGrid.TryGetValue(new Vector2Int(x, absorbedPos.y), out var voxel) && !voxel.Data.isClaimed)
                {
                    if (!voxel.IsExposedFrom(ExposeDirection.Right))
                    {
                        voxel.SetExposed(ExposeDirection.Right);
                        InsertSorted(exposedVoxels[ExposeDirection.Right], voxel, ExposeDirection.Right);
                    }
                    break;
                }
            }
        }
    }
}
