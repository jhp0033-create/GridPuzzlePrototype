using UnityEngine;
using DG.Tweening;

namespace GridPuzzle.UI
{
    public class LogoScript : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float floatAmplitude = 5f;
        [SerializeField] private float floatDuration = 2.0f;
        [SerializeField] private float pulseScale = 1.05f;
        [SerializeField] private float pulseDuration = 1.2f;

        private RectTransform rectTransform;

        // Caches the RectTransform used by the menu logo tweens.
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        // Starts the logo animation once the UI transform is ready.
        private void Start()
        {
            if (rectTransform != null)
            {
                StartLogoAnimation();
            }
        }

        // Runs the floating and pulsing loop used on the title logo.
        private void StartLogoAnimation()
        {
            // Reset state and ensure no X/Y rotation residue
            rectTransform.DOKill();
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            
            // 1. Floating Animation (Up and Down) - Slightly faster
            rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + floatAmplitude, floatDuration)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);

            // 2. Pulsing Animation (Scale) - Re-added as requested
            rectTransform.DOScale(Vector3.one * pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
                
        }

        // Stops any active tweens before the logo object is destroyed.
        private void OnDestroy()
        {
            rectTransform.DOKill();
        }
    }
}
