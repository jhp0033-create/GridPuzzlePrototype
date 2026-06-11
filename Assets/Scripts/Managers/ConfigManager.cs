using UnityEngine;
using GridPuzzle.Data;

namespace GridPuzzle.Managers
{
    public class ConfigManager : MonoBehaviour
    {
        private static ConfigManager _instance;
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ConfigManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ConfigManager");
                        _instance = go.AddComponent<ConfigManager>();
                        // LoadConfig is called in Awake
                    }
                }
                return _instance;
            }
        }

        private GameConfigData config; // Non-serialized to force file load
        public GameConfigData Config => config;

        // Keeps a single live config instance and loads JSON-backed values on startup.
        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
                transform.SetParent(null); // Ensure this object is a root before persisting it.
                DontDestroyOnLoad(gameObject);
                LoadConfig();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Loads runtime tuning values from Resources or falls back to defaults.
        public void LoadConfig()
        {
            // Strictly load from Resources to ensure data-driven integrity
            TextAsset jsonFile = Resources.Load<TextAsset>("GameConfig");
            if (jsonFile != null)
            {
                config = GameConfigData.FromJson(jsonFile.text);
                Debug.Log("<color=green>[ConfigManager]</color> Successfully loaded GameConfig.json from Resources.");
            }
            else
            {
                config = new GameConfigData(); // Hardcoded defaults in class definition
                Debug.LogWarning("[ConfigManager] GameConfig.json not found! Using fallback defaults.");
            }
        }
    }
}
