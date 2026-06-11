using UnityEngine;
using GridPuzzle.Data;
using GridPuzzle.Managers;
using DG.Tweening;

namespace GridPuzzle.Core
{
    public class VoxelModel : MonoBehaviour
    {
        [Header("Data Reference")]
        [SerializeField] private VoxelData data;
        
        [Header("Rendering")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Animator animator;
        
        // Use property IDs for better performance and reliability
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); 
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private MaterialPropertyBlock propBlock;
        private bool hasBeenVisuallyExposed = false;
        private MeshRenderer[] allRenderers;

        public VoxelData Data => data;

        // Caches renderer and animator references used during pooled reuse.
        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            allRenderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        // Stops active tweens and coroutines before the voxel returns to the pool.
        private void OnDisable()
        {
            transform.DOKill();
            StopAllCoroutines();
        }

        // Resets pooled voxel state and reapplies its current color data.
        public void Initialize(VoxelData voxelData, Color color)
        {
            this.data = voxelData;
            hasBeenVisuallyExposed = false;
            
            // Ensure any previous animations are killed before resetting visuals
            transform.DOKill();
            
            // Unconditional color and outline application
            ApplyVoxelVisuals(color);
        }

        // Starts the exposed animation only once for a newly revealed voxel.
        public void TriggerExposed()
        {
            if (!hasBeenVisuallyExposed)
            {
                hasBeenVisuallyExposed = true;
                if (animator != null)
                {
                    // If the object was just activated this frame, the animator might ignore the trigger.
                    // Using a short coroutine ensures the animator is ready to receive triggers.
                    if (gameObject.activeInHierarchy)
                    {
                        StartCoroutine(TriggerAfterFrame());
                    }
                }
            }
        }

        // Defers the trigger by one frame so the animator is ready to receive it.
        private System.Collections.IEnumerator TriggerAfterFrame()
        {
            yield return new WaitForEndOfFrame();
            if (animator != null)
            {
                animator.SetTrigger("Exposed");
            }
            
            var config = ConfigManager.Instance.Config;
            if (config == null) yield break; // Safety check
            
            // Calm left/right wiggle (씰룩씰룩) effect with randomized noise to break monotony
            transform.DOKill();
            
            float randomDuration = UnityEngine.Random.Range(config.wiggleMinDuration, config.wiggleMaxDuration);
            float randomDelay = UnityEngine.Random.Range(0f, config.wiggleMaxDelay);
            float randomAngle = UnityEngine.Random.Range(config.wiggleMinAngle, config.wiggleMaxAngle);
            
            // Set initial slightly tilted state
            transform.localEulerAngles = new Vector3(0, 0, -randomAngle);
            
            // Gently swing back and forth on the Z-axis
            transform.DOLocalRotate(new Vector3(0, 0, randomAngle), randomDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(randomDelay);
                
            // Combine with a very subtle vertical squish to give it life
            transform.DOScale(new Vector3(1.05f, 0.95f, 1.05f), randomDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(randomDelay);
        }

        // Applies runtime palette colors while preserving outline and eye materials.
        private void ApplyVoxelVisuals(Color targetColor)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            // Support new FBX structure: 2 Materials (Base, Outline) and Leg_L/Leg_R child objects
            if (allRenderers == null) return;
            
            foreach (var r in allRenderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    // Preserve the inner outline material of the FBX
                    if (mats[i].name.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    r.GetPropertyBlock(propBlock, i);

                    // Force eye materials to be black
                    if (mats[i].name.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        propBlock.SetColor(BaseColorId, Color.black);
                    }
                    else
                    {
                        // Apply BaseColor to Leg_L, Leg_R, and the main body
                        propBlock.SetColor(BaseColorId, targetColor);
                    }

                    r.SetPropertyBlock(propBlock, i);
                }
            }
        }

        // Adds an exposed side flag without clearing existing side state.
        public void SetExposed(ExposeDirection direction)
        {
            data.exposedFaces |= direction;
        }

        // Removes a specific exposed side flag from the voxel bitmask.
        public void ClearExposed(ExposeDirection direction)
        {
            data.exposedFaces &= ~direction;
        }

        // Returns whether the voxel is currently exposed from the requested side.
        public bool IsExposedFrom(ExposeDirection direction)
        {
            return (data.exposedFaces & direction) != 0;
        }
    }
}
