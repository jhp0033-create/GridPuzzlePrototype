using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using DG.Tweening;
using GridPuzzle.Data;
using GridPuzzle.Managers;

namespace GridPuzzle.Core
{
    public class VehicleModel : MonoBehaviour
    {
        [SerializeField] private VehicleData data;
        [SerializeField] private GameObject modelRoot;
        [SerializeField] private GameObject effectRoot; // Separate effect root to avoid DOTween noise but follow rotation
        [SerializeField] private TextMeshPro quotaText;

        [Header("Effects")]
        [SerializeField] private TrailRenderer[] trails;
        [SerializeField] private ParticleSystem idleParticle;
        [SerializeField] private GameObject shadowObject;

        public Transform QuotaTextTransform => quotaText != null ? quotaText.transform : null;

        private Vector3 initialTextPos;
        private Quaternion initialTextRot;
        private MeshRenderer[] cachedRenderers;
        private MaterialPropertyBlock cachedPropBlock;

        // Caches child references used by runtime state and animation updates.
        private void Awake()
        {
            if (quotaText != null)
            {
                initialTextPos = quotaText.transform.localPosition;
                initialTextRot = quotaText.transform.localRotation;
            }
            if (modelRoot == null) modelRoot = this.gameObject;
            cachedRenderers = modelRoot.GetComponentsInChildren<MeshRenderer>();
            cachedPropBlock = new MaterialPropertyBlock();
        }

        private struct VoxelClaim
        {
            public VoxelModel model;
            public ExposeDirection dir;
        }

        // Segment-separated queues to prevent "vacuuming" from future waypoints
        private Dictionary<int, Queue<VoxelClaim>> segmentTargets = new Dictionary<int, Queue<VoxelClaim>>();
        private int pendingAbsorptions = 0;
        
        public VehicleData Data => data;

        // Applies visuals and effect toggles for the current vehicle state.
        public void SetState(VehicleState newState)
        {
            data.state = newState;
            
            bool isIdle = (newState == VehicleState.InQueue || newState == VehicleState.InStorage);
            bool isMoving = (newState == VehicleState.Moving);

            if (idleParticle != null)
            {
                if (isIdle && !idleParticle.isPlaying) idleParticle.Play();
                else if (!isIdle)
                {
                    // Stop emitting and immediately clear existing particles as requested
                    idleParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (trails != null)
            {
                foreach (var trail in trails)
                {
                    if (trail != null) trail.emitting = isMoving;
                }
            }
            
            if (shadowObject != null)
            {
                shadowObject.SetActive(isIdle);
                if (newState == VehicleState.InStorage)
                {
                    shadowObject.transform.localEulerAngles = new Vector3(
                        shadowObject.transform.localEulerAngles.x,
                        shadowObject.transform.localEulerAngles.y,
                        30f
                    );
                }
                else if (newState == VehicleState.InQueue)
                {
                    shadowObject.transform.localEulerAngles = new Vector3(
                        shadowObject.transform.localEulerAngles.x,
                        shadowObject.transform.localEulerAngles.y,
                        0f
                    );
                }
            }
        }

        // Hides the ground shadow immediately when the vehicle leaves idle states.
        public void HideShadow()
        {
            if (shadowObject != null)
            {
                shadowObject.SetActive(false);
            }
        }

        // Resets pooled vehicle state and reapplies its runtime color and quota.
        public void Initialize(VehicleData vehicleData, Color color)
        {
            this.data = vehicleData;
            pendingAbsorptions = 0;
            if (modelRoot != null)
            {
                modelRoot.transform.localPosition = Vector3.zero;
                modelRoot.transform.localRotation = Quaternion.identity;
                modelRoot.transform.localScale = Vector3.one;
            }
            if (quotaText != null)
            {
                quotaText.transform.localPosition = initialTextPos;
                quotaText.transform.localRotation = initialTextRot;
                quotaText.transform.localScale = Vector3.one;
            }
        
            ApplyColor(color);
            UpdateQuotaDisplay();
            SetState(VehicleState.InQueue);
        }

        // Pushes the runtime body color into the vehicle renderers without duplicating materials.
        private void ApplyColor(Color targetColor)
        {
            if (modelRoot == null) modelRoot = this.gameObject;
            
            MeshRenderer[] renderers = modelRoot.GetComponentsInChildren<MeshRenderer>();
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.Length > 0)
                {
                    // Only apply the color to the first material (index 0)
                    renderer.GetPropertyBlock(propBlock, 0);
                    propBlock.SetColor("_BaseColor", targetColor);
                    renderer.SetPropertyBlock(propBlock, 0);
                }
            }
        }

        // Refreshes the world-space quota label from the live vehicle data.
        public void UpdateQuotaDisplay()
        {
            if (quotaText != null && data != null)
            {
                quotaText.text = data.currentQuota.ToString();
            }
        }

        public ExposeDirection CurrentSegmentFace { get; private set; } = ExposeDirection.None;

        // Finalizes a completed absorption by consuming quota and pending counters.
        public void ProcessAbsorption()
        {
            if (pendingAbsorptions > 0) pendingAbsorptions--;
            
            if (data.currentQuota > 0)
            {
                data.currentQuota--;
                UpdateQuotaDisplay();
            }
        }

        // Converts current travel direction into the matching exposed shell side.
        private ExposeDirection GetRequiredExposeDirection(Vector3 velocity)
        {
            if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.z))
            {
                return velocity.x > 0 ? ExposeDirection.Bottom : ExposeDirection.Top;
            }
            else
            {
                return velocity.z > 0 ? ExposeDirection.Right : ExposeDirection.Left;
            }
        }

        // Plays the voxel-to-vehicle absorption animation and resolves the hit on completion.
        private void AnimateAbsorption(VoxelModel voxel)
        {
            if (Managers.SoundManager.Instance != null) Managers.SoundManager.Instance.PlaySFX(Managers.SoundManager.Instance.absorbSound);

            var config = ConfigManager.Instance.Config;
            Sequence seq = DOTween.Sequence();
            
            // Instantly pop the voxel scale (from config)
            voxel.transform.localScale = Vector3.one * config.voxelPopScale;
            
            // Rotate X by 30 degrees to face the camera, and spin dynamically on the Y axis
            Vector3 targetRot = new Vector3(30f, 360f, 0f);
            
            // Manual lerp for perfect target tracking (following the moving car)
            float elapsed = 0f;
            Vector3 startPos = voxel.transform.position;
            seq.Append(DOTween.To(() => elapsed, x => elapsed = x, 1f, config.voxelAbsorbDuration).SetEase(Ease.InBack).OnUpdate(() => {
                voxel.transform.position = Vector3.Lerp(startPos, transform.position, elapsed);
            }));

            seq.Join(voxel.transform.DOScale(Vector3.zero, config.voxelAbsorbDuration).SetEase(Ease.InBack));
            seq.Join(voxel.transform.DORotate(targetRot, config.voxelAbsorbDuration, RotateMode.FastBeyond360).SetRelative(true));
            
            // Vehicle dynamic scale effect (prevent stacking/accumulating)
            if (modelRoot != null)
            {
                modelRoot.transform.DOKill();
                modelRoot.transform.localScale = Vector3.one * 0.8f; // Reset to base rail scale
                modelRoot.transform.DOScale(Vector3.one * 1.1f, 0.15f).SetLoops(2, LoopType.Yoyo);
            }
            
            seq.OnComplete(() => {
                if (Managers.SoundManager.Instance != null) Managers.SoundManager.Instance.PlaySFX(Managers.SoundManager.Instance.hitSound);
                ProcessAbsorption();
                VoxelGridManager.Instance.AbsorbVoxel(voxel);
            });
        }

        public Transform ModelRoot => modelRoot != null ? modelRoot.transform : transform;

        // Starts a full rail lap for the deployed vehicle.
        public void StartLap(Transform[] waypoints, float moveSpeed, Action onLapComplete)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                onLapComplete?.Invoke();
                return;
            }
            StartCoroutine(MoveAlongRailRoutine(waypoints, moveSpeed, onLapComplete));
        }

        // Immediately stops active tweens, coroutines, and looping vehicle effects.
        public void StopMotion()
        {
            StopAllCoroutines();
            transform.DOKill();

            if (modelRoot != null)
            {
                modelRoot.transform.DOKill();
            }

            if (effectRoot != null)
            {
                effectRoot.transform.DOKill();
            }

            if (quotaText != null)
            {
                quotaText.transform.DOKill();
            }

            if (idleParticle != null)
            {
                idleParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (trails != null)
            {
                foreach (var trail in trails)
                {
                    if (trail != null) trail.emitting = false;
                }
            }
        }

        // Moves along the rail, reserves segment targets, and absorbs voxels in order.
        private IEnumerator MoveAlongRailRoutine(Transform[] waypoints, float moveSpeed, Action onComplete)
        {
            data.state = VehicleState.Moving;

            int unassignedQuota = data.currentQuota;
            segmentTargets.Clear();

            // 1. PRE-CLAIM BY SEGMENT: Strictly isolate targets to their intended rail section
            for (int i = 1; i < waypoints.Length; i++)
            {
                segmentTargets[i] = new Queue<VoxelClaim>();
                if (unassignedQuota <= 0) continue;
                
                Vector3 segmentDir = (waypoints[i].position - waypoints[i-1].position).normalized;
                ExposeDirection reqDir = GetRequiredExposeDirection(segmentDir);
                
                var segmentClaims = VoxelGridManager.Instance.PreClaimVoxels(data.colorType, reqDir, unassignedQuota, waypoints[i-1].position);
                
                foreach (var voxel in segmentClaims)
                {
                    segmentTargets[i].Enqueue(new VoxelClaim { model = voxel, dir = reqDir });
                    unassignedQuota--;
                }
                
                if (segmentClaims.Count > 0)
                    Debug.Log($"[Segment {i}] Reserved {segmentClaims.Count} voxels for {reqDir}");
            }

            int currentIndex = 1;
            pendingAbsorptions = 0; // Fix shadowing to correctly reference the class variable
            
            Queue<VoxelClaim> currentQueue = null;
            segmentTargets.TryGetValue(currentIndex, out currentQueue);
            
            while (currentIndex < waypoints.Length)
            {
                Vector3 targetPos = waypoints[currentIndex].position;
                Vector3 desiredDirection = (targetPos - transform.position).normalized;
                
                // Strictly update and enforce the CurrentSegmentFace state based on the actual rail path
                CurrentSegmentFace = GetRequiredExposeDirection(desiredDirection);
                
                // Interpolate rotation quickly to face the target waypoint for a snappy but smooth turn
                if (desiredDirection != Vector3.zero)
                {
                    Vector3 lookDir = desiredDirection; lookDir.y = 0;
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    ModelRoot.rotation = Quaternion.Slerp(ModelRoot.rotation, targetRot, Time.deltaTime * 25f);
                    if (effectRoot != null)
                    {
                        effectRoot.transform.rotation = Quaternion.Slerp(effectRoot.transform.rotation, targetRot, Time.deltaTime * 25f);
                    }
                }

                // Move linearly towards the waypoint, guaranteeing absolutely 0 overshoot
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

                // 2. SEGMENT-LOCKED ABSORPTION: Only check voxels for the CURRENT rail section using the cached queue
                if (currentQueue != null && currentQueue.Count > 0)
                {
                    VoxelClaim next = currentQueue.Peek();
                    bool shouldAbsorb = false;

                    // Axis-Aligned "Passing" Logic using the STRICT CURRENT STATE of the vehicle
                    switch (CurrentSegmentFace)
                    {
                        case ExposeDirection.Bottom: // Moving +X
                            shouldAbsorb = transform.position.x >= next.model.transform.position.x - 0.5f;
                            break;
                        case ExposeDirection.Top:    // Moving -X
                            shouldAbsorb = transform.position.x <= next.model.transform.position.x + 0.5f;
                            break;
                        case ExposeDirection.Right:  // Moving +Z
                            shouldAbsorb = transform.position.z >= next.model.transform.position.z - 0.5f;
                            break;
                        case ExposeDirection.Left:   // Moving -Z
                            shouldAbsorb = transform.position.z <= next.model.transform.position.z + 0.5f;
                            break;
                    }

                    if (shouldAbsorb)
                    {
                        currentQueue.Dequeue();
                        AnimateAbsorption(next.model);
                        pendingAbsorptions++;
                    }
                }
                
                // Break out of the movement loop immediately ONLY if quota is physically fulfilled
                if (data.currentQuota <= 0)
                {
                     break;
                }
                
                // Advance to next waypoint if we perfectly reached the current one
                if (Vector3.Distance(transform.position, targetPos) < 0.001f)
                {
                    currentIndex++;
                    if (currentIndex < waypoints.Length)
                    {
                        segmentTargets.TryGetValue(currentIndex, out currentQueue);
                    }
                }
                else
                {
                    yield return null;
                }
            }
            
            // Wait for all pending absorption animations to complete before invoking the callback
            while (data.currentQuota > 0 && pendingAbsorptions > 0)
            {
                yield return null;
            }
            
            // Ensure precise positioning at the end of the lap ONLY if we didn't finish early
            if (data.currentQuota > 0)
            {
                transform.position = waypoints[waypoints.Length - 1].position;
            }
            
            onComplete?.Invoke();
        }
    }
}
