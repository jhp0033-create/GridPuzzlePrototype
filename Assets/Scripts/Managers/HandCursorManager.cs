using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GridPuzzle.Core;
using System;

namespace GridPuzzle.Managers
{
    public class HandCursorManager : MonoBehaviour
    {
        public static HandCursorManager Instance { get; private set; }

        [SerializeField] private RectTransform handImage;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private float moveSpeed = 1500f; // Pixels per second
        
        private Camera mainCam;
        private bool isInteracting = false;

        // Caches the main camera for world-to-canvas coordinate conversion.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            mainCam = Camera.main;
        }

        // Subscribes after scene init and snaps the cursor to the first valid vehicle.
        private void Start()
        {
            // Wait for VehicleManager to initialize vehicles before snapping
            if (VehicleManager.Instance != null)
            {
                VehicleManager.Instance.OnVehiclesInitialized += ResetToQueue;
            }
            
            // Call explicitly just in case GameManager already initialized them this frame
            ResetToQueue();
            StartIdleAnimation();
        }

        // Releases event subscriptions when the cursor helper is destroyed.
        private void OnDestroy()
        {
            if (VehicleManager.Instance != null)
            {
                VehicleManager.Instance.OnVehiclesInitialized -= ResetToQueue;
            }
        }

        // Plays a subtle idle pulse while the cursor is waiting for input.
        private void StartIdleAnimation()
        {
            if (handImage == null) return;
            handImage.DOKill();
            handImage.localScale = Vector3.one;
            // Gentle idle floating effect (scale based so it doesn't fight position)
            handImage.DOScale(Vector3.one * 1.1f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        // Moves the cursor back to the first selectable queued vehicle.
        public void ResetToQueue()
        {
            if (handImage == null) return;

            VehicleModel first = VehicleManager.Instance.GetFirstQueuedVehicle();
            if (first != null && mainCam != null && rootCanvas != null)
            {
                handImage.gameObject.SetActive(true);
                Vector2 screenPos = mainCam.WorldToScreenPoint(first.transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvas.transform as RectTransform, screenPos, rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out Vector2 localPos);
                handImage.localPosition = localPos;
                StartIdleAnimation(); // Immediately start the idle bounce
            }
            else
            {
                handImage.gameObject.SetActive(false);
            }
        }

        // Animates the cursor toward a target before invoking the click callback.
        public void PointAndClick(VehicleModel target, Action onClickComplete)
        {
            if (isInteracting || handImage == null || rootCanvas == null)
            {
                // If already moving, just execute immediate (or ignore)
                onClickComplete?.Invoke();
                return;
            }

            isInteracting = true;
            handImage.DOKill(); // Stop idle animation
            handImage.localScale = Vector3.one;

            Vector2 screenPos = mainCam.WorldToScreenPoint(target.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvas.transform as RectTransform, screenPos, rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out Vector2 localPos);
            
            float distance = Vector2.Distance(handImage.localPosition, localPos);
            float duration = Mathf.Clamp(distance / moveSpeed, 0.1f, 0.3f);

            Sequence seq = DOTween.Sequence();
            
            // 1. Move to vehicle using DOAnchorPos for UI coordinates
            seq.Append(handImage.DOLocalMove(localPos, duration).SetEase(Ease.OutQuad));
            
            // 2. Press animation (scale down and up)
            seq.Append(handImage.DOScale(Vector3.one * 0.7f, 0.1f).SetEase(Ease.InQuad));
            seq.Append(handImage.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutQuad));
            
            // 3. Complete and trigger selection
            seq.OnComplete(() => {
                isInteracting = false;
                StartIdleAnimation();
                onClickComplete?.Invoke();
            });
        }
    }
}
