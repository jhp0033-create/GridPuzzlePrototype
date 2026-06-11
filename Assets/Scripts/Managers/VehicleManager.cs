using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using GridPuzzle.Data;
using GridPuzzle.Core;
using TMPro;

namespace GridPuzzle.Managers
{
    public class VehicleManager : MonoBehaviour
    {
        public static VehicleManager Instance { get; private set; }

        public event System.Action OnQueueUpdated;
        public event System.Action OnVehiclesInitialized;

        [System.Serializable]
        public struct QueueSetting
        {
            public Transform anchor;
            public Queue<VehicleModel> activeVehicles;
        }

        [System.Serializable]
        public struct VisualStateSetting
        {
            public Vector3 localScale;
            public Vector3 localEulerAngles;
        }

        [Header("Visual State Settings")]
        [SerializeField] private VisualStateSetting queueVisuals = new VisualStateSetting { localScale = Vector3.one, localEulerAngles = new Vector3(15, 0, 0) };
        [SerializeField] private VisualStateSetting railVisuals = new VisualStateSetting { localScale = Vector3.one * 0.8f, localEulerAngles = Vector3.zero };
        [SerializeField] private VisualStateSetting storageVisuals = new VisualStateSetting { localScale = Vector3.one, localEulerAngles = new Vector3(0, -30, 0) };

        [Header("Queue Lane Settings")]
        [SerializeField] private QueueSetting[] lanes = new QueueSetting[3];
    

        [Header("Rail Settings")]
        [SerializeField] private Transform[] railWaypoints = new Transform[5];
        [SerializeField] private int maxVehiclesOnRail = 5;
        [SerializeField] private TMPro.TextMeshProUGUI activeVehiclesText;
        
        private int activeVehiclesOnRail = 0;

        // Refreshes the live rail occupancy text shown in the scene UI.
        private void UpdateActiveVehiclesText()
        {
            if (activeVehiclesText != null)
            {
                activeVehiclesText.text = $"{activeVehiclesOnRail}/{maxVehiclesOnRail}";
            }
        }

        [Header("Storage Settings")]
        [SerializeField] private Vector2 storageSpacing = new Vector2(2f, 0f);
        [SerializeField] private Vector3 storageOffset = Vector3.zero;
        [SerializeField] private Transform storageParent;
        private List<VehicleModel> storageSlots = new List<VehicleModel>();
        private List<VehicleModel> deployedVehicles = new List<VehicleModel>();

        [Header("Effects")]
        [SerializeField] private GameObject completionEffectPrefab;
        private Queue<GameObject> effectPool = new Queue<GameObject>();

        // Initializes queue containers and one-off UI material overrides.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializeLaneLists();

            // Ensure the Rail text always renders on top of 3D objects even in Camera/World space Canvas.
            // By instantiating a new Material, it guarantees NO OTHER UI elements are affected.
            if (activeVehiclesText != null)
            {
                Material mat = new Material(activeVehiclesText.fontMaterial);
                Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
                if (overlayShader != null)
                {
                    mat.shader = overlayShader;
                }
                activeVehiclesText.fontMaterial = mat;
            }
        }

        // Ensures each queue lane has a backing runtime container.
        private void InitializeLaneLists()
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].activeVehicles == null)
                    lanes[i].activeVehicles = new Queue<VehicleModel>();
            }
        }

        // Clears existing vehicles and rebuilds the queue layout from level data.
        public void InitializeVehicles(LevelData levelData)
        {
            ClearAll();
            activeVehiclesOnRail = 0;
            
            if (levelData == null) return;

            bool hasFixedLanes = false;
            if (levelData.selectionLanes != null)
            {
                foreach (var lane in levelData.selectionLanes)
                {
                    if (lane != null && lane.vehicles != null && lane.vehicles.Count > 0)
                    {
                        hasFixedLanes = true;
                        break;
                    }
                }
            }

            if (hasFixedLanes)
            {
                // Fixed Generation from LevelData
                for (int col = 0; col < levelData.selectionLanes.Length && col < lanes.Length; col++)
                {
                    foreach (var vehicleData in levelData.selectionLanes[col].vehicles)
                    {
                        SpawnVehicleInQueue(vehicleData, col, levelData.palette);
                    }
                }
            }
            else
            {
                // Auto-Generation based on Voxel Data
                AutoGenerateVehicles(levelData);
            }
            
            UpdateLayouts(false); // Static layout on initialization
            OnVehiclesInitialized?.Invoke(); // Trigger Hand Cursor initialization
        }

        // Derives a usable queue from voxel counts when fixed lane data is absent.
        private void AutoGenerateVehicles(LevelData levelData)
        {
            Dictionary<ColorType, int> colorCounts = new Dictionary<ColorType, int>();
            foreach (var voxel in levelData.voxels)
            {
                if (voxel.colorType == ColorType.None) continue;
                
                if (!colorCounts.ContainsKey(voxel.colorType))
                    colorCounts[voxel.colorType] = 0;
                
                colorCounts[voxel.colorType]++;
            }

            List<VehicleData> generatedVehicles = new List<VehicleData>();
            int vehicleIdCounter = 0;

            foreach (var kvp in colorCounts)
            {
                ColorType type = kvp.Key;
                int totalQuota = kvp.Value;

                // Safely distribute totalQuota into evenly sized buckets (max 20 per vehicle)
                // This guarantees quotas are always solidly between 10~20 without tiny "leftover" vehicles 
                // (Unless the absolute total for that color on the map is less than 10)
                int numVehicles = Mathf.Max(1, Mathf.CeilToInt(totalQuota / 20f));
                
                int baseQuota = totalQuota / numVehicles;
                int remainder = totalQuota % numVehicles;

                for (int i = 0; i < numVehicles; i++)
                {
                    int quota = baseQuota + (i < remainder ? 1 : 0);
                    generatedVehicles.Add(new VehicleData($"auto_veh_{vehicleIdCounter++}", type, quota));
                }
            }

            // Shuffle the generated vehicles
            for (int i = 0; i < generatedVehicles.Count; i++)
            {
                VehicleData temp = generatedVehicles[i];
                int randomIndex = Random.Range(i, generatedVehicles.Count);
                generatedVehicles[i] = generatedVehicles[randomIndex];
                generatedVehicles[randomIndex] = temp;
            }

            // Distribute evenly across lanes
            for (int i = 0; i < generatedVehicles.Count; i++)
            {
                int colIndex = i % lanes.Length;
                SpawnVehicleInQueue(generatedVehicles[i], colIndex, levelData.palette);
            }
            
            Debug.Log($"[VehicleManager] Auto-generated {generatedVehicles.Count} vehicles based on voxel counts.");
        }

        // Pulls a pooled vehicle, colors it, and inserts it into a queue lane.
        private void SpawnVehicleInQueue(VehicleData data, int colIndex, Color[] palette)
        {
            VehicleModel model = VehiclePool.Instance.Get();
            Color color = Color.white;
            if (palette != null && (int)data.colorType > 0 && (int)data.colorType <= palette.Length)
            {
                color = palette[(int)data.colorType - 1];
            }

            model.Initialize(data, color);
            model.transform.SetParent(lanes[colIndex].anchor != null ? lanes[colIndex].anchor : transform);
            
            if (model.ModelRoot != null)
            {
                model.ModelRoot.localScale = queueVisuals.localScale;
                model.ModelRoot.localEulerAngles = queueVisuals.localEulerAngles;
            }
            
            lanes[colIndex].activeVehicles.Enqueue(model);
        }

        // Returns whether a vehicle is currently a legal player selection target.
        public bool IsSelectable(VehicleModel vehicle)
        {
            if (activeVehiclesOnRail >= maxVehiclesOnRail) return false;

            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].activeVehicles.Count > 0 && lanes[i].activeVehicles.Peek() == vehicle)
                {
                    return true;
                }
            }

            if (storageSlots.Contains(vehicle))
            {
                return true;
            }

            return false;
        }

        // Routes a legal selection into queue or storage deployment flow.
        public void SelectVehicle(VehicleModel vehicle)
        {
            // Only allow manual selection during active Playing state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            {
                return;
            }

            var config = ConfigManager.Instance.Config;
            if (activeVehiclesOnRail >= config.maxVehiclesOnRail)
            {
                Debug.Log($"[VehicleManager] Cannot deploy {vehicle.gameObject.name}. Max vehicles ({config.maxVehiclesOnRail}) already on rail.");
                return; // Enforce the 5 concurrent vehicles limit
            }

            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].activeVehicles.Count > 0 && lanes[i].activeVehicles.Peek() == vehicle)
                {
                    DeployVehicle(vehicle, i);
                    return;
                }
            }

            if (storageSlots.Contains(vehicle))
            {
                DeployVehicle(vehicle, -1);
            }
        }

        // Moves a selected vehicle from idle space onto the rail and starts its lap.
        private void DeployVehicle(VehicleModel vehicle, int columnIndex)
        {
            if (!IsGameplayActive())
            {
                vehicle.StopMotion();
                return;
            }

            var config = ConfigManager.Instance.Config;
            vehicle.HideShadow(); // Hide shadow immediately upon starting movement
            
            if (columnIndex >= 0) 
            {
                lanes[columnIndex].activeVehicles.Dequeue();
                UpdateLaneLayout(columnIndex, true); // Only update the lane that changed
            }
            else 
            {
                storageSlots.Remove(vehicle);
                UpdateStorageLayout(true);
            }
            
            activeVehiclesOnRail++;
            deployedVehicles.Add(vehicle); // Track the vehicle on rail
            UpdateActiveVehiclesText();
            OnQueueUpdated?.Invoke();
            
            if (railWaypoints != null && railWaypoints.Length == 5 && railWaypoints[0] != null)
            {
                vehicle.transform.SetParent(railWaypoints[0].parent);
                
                Sequence deploySeq = DOTween.Sequence();
                deploySeq.Append(vehicle.transform.DOMove(railWaypoints[0].position, config.deploymentMoveDuration).SetEase(Ease.OutQuad));
                
                if (vehicle.ModelRoot != null && railWaypoints[1] != null)
                {
                    Vector3 lookPos = railWaypoints[1].position;
                    lookPos.y = vehicle.ModelRoot.position.y;
                    Vector3 dir = (lookPos - vehicle.ModelRoot.position).normalized;
                    float targetY = Quaternion.LookRotation(dir).eulerAngles.y;
                    Vector3 targetRot = new Vector3(railVisuals.localEulerAngles.x, targetY, railVisuals.localEulerAngles.z);

                    deploySeq.Join(vehicle.ModelRoot.DOLocalRotate(targetRot, config.deploymentRotateDuration));
                    deploySeq.Join(vehicle.ModelRoot.DOScale(railVisuals.localScale, config.deploymentMoveDuration));
                }
                
                deploySeq.OnComplete(() => {
                    if (!IsGameplayActive())
                    {
                        vehicle.StopMotion();
                        return;
                    }

                    vehicle.SetState(VehicleState.Moving);
                    vehicle.StartLap(railWaypoints, config.vehicleRailSpeed, () => HandleLapCompletion(vehicle));
                });
            }
            else
            {
                Debug.LogWarning("Rail Waypoints are not properly assigned (need 5) in VehicleManager!");

                if (!IsGameplayActive())
                {
                    vehicle.StopMotion();
                    return;
                }

                vehicle.SetState(VehicleState.Moving);
                vehicle.StartLap(null, config.vehicleRailSpeed, () => HandleLapCompletion(vehicle));
            }
        }

        // Returns the first visible queue-head vehicle for tutorial cursor placement.
        public VehicleModel GetFirstQueuedVehicle()
        {
            foreach (var lane in lanes)
            {
                if (lane.activeVehicles != null && lane.activeVehicles.Count > 0)
                {
                    return lane.activeVehicles.Peek();
                }
            }
            return null;
        }

        // Reuses or spawns a completion effect instance at the requested position.
        private GameObject GetCompletionEffect(Vector3 position)
        {
            if (completionEffectPrefab == null) return null;
            
            GameObject effect;
            if (effectPool.Count > 0)
            {
                effect = effectPool.Dequeue();
                effect.transform.position = position;
                effect.SetActive(true);
            }
            else
            {
                effect = Instantiate(completionEffectPrefab, position, Quaternion.identity);
            }

            // Return to pool after animation (assuming particle systems take ~2 seconds max)
            DOVirtual.DelayedCall(2.0f, () => {
                if (effect != null)
                {
                    effect.SetActive(false);
                    effectPool.Enqueue(effect);
                }
            });

            return effect;
        }

        // Resolves the vehicle outcome after a lap, including storage or clear effects.
        private void HandleLapCompletion(VehicleModel vehicle)
        {
            if (!IsGameplayActive())
            {
                vehicle.StopMotion();
                return;
            }

            deployedVehicles.Remove(vehicle);
            activeVehiclesOnRail--; // Vehicle left the rail
            UpdateActiveVehiclesText(); // Update the 3D text
            
            if (vehicle.Data.currentQuota > 0)
            {
                // If quota remains, try to store it
                AddToStorage(vehicle);
            }
            else
            {
                // Quota met, clear vehicle with a celebratory spin effect
                vehicle.SetState(VehicleState.Completed);
                Debug.Log($"<color=cyan>[Clear Effect]</color> Vehicle {vehicle.gameObject.name} cleared! Playing spin effect.");
                
                // Spawn effect slightly higher so it's not hidden inside/under the vehicle
                GetCompletionEffect(vehicle.transform.position + Vector3.up * 1.0f);
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.lapCompleteSound);
                Sequence clearSeq = DOTween.Sequence();
                // Camera shake effect immediately on clear, quick and subtle
                if (Camera.main != null)
                {
                    Camera.main.transform.DOKill();
                    clearSeq.Append(Camera.main.transform.DOShakePosition(0.15f, 0.1f, 20, 90f, false, true));
                }

                // Spin 720 degrees (2 laps) on Y axis, move up slightly, and shrink to zero like it's vanishing
                clearSeq.Join(vehicle.ModelRoot.DOLocalRotate(new Vector3(0, 720, 0), 0.8f, RotateMode.FastBeyond360).SetRelative(true).SetEase(Ease.InOutQuad));
                clearSeq.Join(vehicle.ModelRoot.DOLocalMoveY(vehicle.ModelRoot.localPosition.y + 1.5f, 0.8f).SetEase(Ease.OutQuad));
                clearSeq.Join(vehicle.ModelRoot.DOScale(Vector3.zero, 0.8f).SetEase(Ease.InBack));

                if (vehicle.QuotaTextTransform != null)
                {
                    // Animate the text independently so it keeps its fixed rotation facing the camera, 
                    // but use world DOMoveY so it jumps strictly upward despite its -60 degree local rotation.
                    clearSeq.Join(vehicle.QuotaTextTransform.DOMoveY(vehicle.QuotaTextTransform.position.y + 1.0f, 0.8f).SetEase(Ease.OutQuad));
                    clearSeq.Join(vehicle.QuotaTextTransform.DOScale(Vector3.zero, 0.8f).SetEase(Ease.InBack));
                }
                
                clearSeq.OnComplete(() => {
                    VehiclePool.Instance.ReturnToPool(vehicle);
                    
                    var config = ConfigManager.Instance.Config;
                    // Check if threshold vehicles remain to speed up the game
                    int vehiclesInLanes = 0;
                    foreach (var lane in lanes) vehiclesInLanes += lane.activeVehicles.Count;
                    int totalRemaining = activeVehiclesOnRail + storageSlots.Count + vehiclesInLanes;

                    if (totalRemaining <= config.endgameSpeedThresholdCount && Time.timeScale < config.endgameTimeScale)
                    {
                        Debug.Log($"<color=magenta>[Speed Up]</color> {totalRemaining} or fewer vehicles remaining. Increasing game speed to {config.endgameTimeScale}x!");
                        Time.timeScale = config.endgameTimeScale;
                    }
                });
            }
        }

        // Parks an unfinished vehicle in storage or triggers failure if storage is full.
        public void AddToStorage(VehicleModel vehicle)
        {
            if (!IsGameplayActive())
            {
                vehicle.StopMotion();
                return;
            }

            var config = ConfigManager.Instance.Config;
            if (storageSlots.Count < config.maxStorageSlots)
            {
                storageSlots.Add(vehicle);
                vehicle.SetState(VehicleState.InStorage);
                vehicle.transform.SetParent(storageParent);
                
                if (vehicle.ModelRoot != null)
                {
                    vehicle.ModelRoot.DOLocalRotate(storageVisuals.localEulerAngles, 0.3f);
                    
                    Sequence scaleSeq = DOTween.Sequence();
                    scaleSeq.AppendInterval(0.2f);
                   
                    scaleSeq.Append(vehicle.ModelRoot.DOScale(storageVisuals.localScale * 1.2f, 0.15f).SetEase(Ease.OutQuad));
                    scaleSeq.Append(vehicle.ModelRoot.DOScale(storageVisuals.localScale, 0.15f).SetEase(Ease.InBack));
                }
                
                UpdateStorageLayout(true);
            }
            else
            {
                // Only trigger GameOver if we haven't won yet
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Victory)
                {
                    Debug.Log("<color=red>[VehicleManager]</color> Storage Full - Triggering Game Over.");
                    GameManager.Instance.ChangeState(GameManager.GameState.GameOver);
                }
                else
                {
                    // If we already won, just stop the vehicle to avoid overflowing UI
                    vehicle.StopMotion();
                }
            }
        }

        // Rebuilds queue and storage layouts after state changes.
        private void UpdateLayouts(bool animate = true)
        {
            for (int i = 0; i < lanes.Length; i++) UpdateLaneLayout(i, animate);
            UpdateStorageLayout(animate);
        }

        // Repositions all vehicles in one queue lane using current spacing rules.
        private void UpdateLaneLayout(int colIndex, bool animate)
        {
            var config = ConfigManager.Instance.Config;
            float spacing = config.laneVerticalSpacing; // Cache spacing to avoid repeated getter calls
            
            int row = 0;
            foreach (var vehicle in lanes[colIndex].activeVehicles)
            {
                Vector3 targetPos = new Vector3(0, 0, -row * spacing);
                if (animate)
                {
                    vehicle.transform.DOLocalMove(targetPos, 0.15f).SetEase(Ease.OutQuad);
                }
                else
                {
                    vehicle.transform.localPosition = targetPos;
                }
                row++;
            }
        }

        // Repositions stored vehicles according to current storage offsets.
        private void UpdateStorageLayout(bool animate)
        {
            for (int i = 0; i < storageSlots.Count; i++)
            {
                Vector3 targetPos = new Vector3(i * storageSpacing.x, 0, 0) + storageOffset;
                if (animate)
                {
                    storageSlots[i].transform.DOLocalMove(targetPos, 0.3f).SetEase(Ease.OutQuad);
                }
                else
                {
                    storageSlots[i].transform.localPosition = targetPos;
                }
            }
        }

        // Kills active motion when gameplay ends in failure.
        public void StopAllVehicles()
        {
            Debug.Log("<color=red>[VehicleManager]</color> Game Over - Stopping all vehicles.");

            // Stop every live vehicle, including queue, storage, rail, and deployment-in-progress states.
            VehicleModel[] allVehicles = FindObjectsOfType<VehicleModel>(true);
            foreach (var vehicle in allVehicles)
            {
                if (vehicle == null || !vehicle.gameObject.scene.IsValid()) continue;
                vehicle.StopMotion();
            }
        }

        // Returns whether vehicle flow is still allowed to advance gameplay state.
        // Returns true during Victory to allow remaining vehicles to finish their animations/laps.
        private bool IsGameplayActive()
        {
            if (GameManager.Instance == null) return true;
            GameManager.GameState state = GameManager.Instance.CurrentState;
            return state == GameManager.GameState.Playing || state == GameManager.GameState.Victory;
        }

        // Returns every managed vehicle to the pool before rebuilding the layout.
        private void ClearAll()
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                foreach (var v in lanes[i].activeVehicles) VehiclePool.Instance.ReturnToPool(v);
                lanes[i].activeVehicles.Clear();
            }
            
            foreach (var v in storageSlots) VehiclePool.Instance.ReturnToPool(v);
            storageSlots.Clear();

            foreach (var v in deployedVehicles) VehiclePool.Instance.ReturnToPool(v);
            deployedVehicles.Clear();
        }
    }
}
