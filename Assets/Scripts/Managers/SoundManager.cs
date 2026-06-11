using UnityEngine;

namespace GridPuzzle.Managers
{
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public static SoundManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SoundManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("SoundManager");
                        _instance = go.AddComponent<SoundManager>();
                        Debug.LogWarning("[SoundManager] Auto-created in scene. Please assign AudioClips in the inspector of the SoundManager prefab!");
                    }
                }
                return _instance;
            }
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;

        [Header("Audio Clips")]
        public AudioClip clickSound;
        public AudioClip absorbSound; // When voxel starts flying
        public AudioClip hitSound;    // When voxel hits the car (quota decreases)
        public AudioClip lapCompleteSound;
        public AudioClip victorySound;
        public AudioClip gameOverSound;

        private float lastAbsorbTime = 0f;

        // Preserves a single audio manager and guarantees an SFX source exists.
        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
                transform.SetParent(null); // Force to root for DontDestroyOnLoad
                DontDestroyOnLoad(gameObject);
                
                // Auto-create audio sources if missing
                if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Plays one-shot sound effects with lightweight absorb-sound throttling.
        public void PlaySFX(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null) 
            {
                Debug.LogWarning("<color=orange>[SoundManager]</color> Attempted to play SFX, but the AudioClip is NULL! Please assign the audio file in the Inspector.");
                return;
            }
            if (sfxSource == null) 
            {
                Debug.LogWarning("<color=orange>[SoundManager]</color> sfxSource is NULL! Cannot play sound.");
                return;
            }

            // Prevent absorb sound stacking using config value
            if (clip == absorbSound)
            {
                var config = ConfigManager.Instance.Config;
                if (Time.time - lastAbsorbTime < config.absorbSoundCooldown) return;
                lastAbsorbTime = Time.time;
            }

            // Debug.Log($"<color=white>[SoundManager]</color> Playing SFX: {clip.name}");
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}
