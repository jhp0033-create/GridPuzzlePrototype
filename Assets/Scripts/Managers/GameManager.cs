using UnityEngine;
using GridPuzzle.Data;
using DG.Tweening;

namespace GridPuzzle.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Ready, Playing, Paused, GameOver, Victory }
        [SerializeField] private GameState currentState;
        public GameState CurrentState => currentState;

        [Header("Initial Level")]
        [SerializeField] private LevelData startLevel;

        [Header("UI Panels")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject gameOverPanel;

        // Persists the top-level game flow manager across scene reloads.
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null); // Force to root for DontDestroyOnLoad
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Boots the first level and hides terminal UI until needed.
        private void Start()
        {
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            LevelManager.Instance.LoadLevel1();
            StartGame();
        }

        // Enters active gameplay after the level has been initialized.
        public void StartGame()
        {
            ChangeState(GameState.Playing);
        }

        // Applies state transitions and triggers matching UI and audio feedback.
        public void ChangeState(GameState newState)
        {
            currentState = newState;
            Debug.Log($"<color=white><b>[GameManager]</b> State -> {newState}</color>");

            if (newState == GameState.Victory)
            {
                Debug.Log("<color=green>🌟 VICTORY! All voxels cleared!</color>");
                DOVirtual.DelayedCall(0.5f, () => {
                    if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.victorySound);
                });
                if (victoryPanel != null) victoryPanel.SetActive(true);
            }
            else if (newState == GameState.GameOver)
            {
                Debug.Log("<color=red>💀 GAME OVER! Storage is full.</color>");
                if (VehicleManager.Instance != null) VehicleManager.Instance.StopAllVehicles();
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.gameOverSound);
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
            }
        }

        // Reloads the current scene and restores the default time scale.
        public void RestartGame()
        {
            Time.timeScale = 1.0f; // Reset speed
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
