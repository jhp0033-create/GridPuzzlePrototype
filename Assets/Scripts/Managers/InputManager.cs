using UnityEngine;
using GridPuzzle.Core;

namespace GridPuzzle.Managers
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [SerializeField] private Camera mainCamera;

        // Initializes the singleton used to route player input.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Polls for the primary click and forwards it into game space.
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandleInput(Input.mousePosition);
            }
        }

        // Resolves a screen click into a selectable vehicle action.
        private void HandleInput(Vector3 inputPos)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            if (mainCamera == null) mainCamera = Camera.main;
            
            Ray ray = mainCamera.ScreenPointToRay(inputPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                VehicleModel vehicle = hit.collider.GetComponent<VehicleModel>();
                if (vehicle != null && VehicleManager.Instance.IsSelectable(vehicle))
                {
                    // Play sound IMMEDIATELY for zero-latency feedback
                    if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSound);

                    // Hand cursor moves to the vehicle and plays press animation, THEN selects it
                    if (HandCursorManager.Instance != null)
                    {
                        HandCursorManager.Instance.PointAndClick(vehicle, () => {
                            VehicleManager.Instance.SelectVehicle(vehicle);
                        });
                    }
                    else
                    {
                        VehicleManager.Instance.SelectVehicle(vehicle);
                    }
                }
            }
        }
    }
}
