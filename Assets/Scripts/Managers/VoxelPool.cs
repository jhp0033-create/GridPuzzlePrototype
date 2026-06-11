using UnityEngine;
using System.Collections.Generic;
using GridPuzzle.Core;

namespace GridPuzzle.Managers
{
    public class VoxelPool : MonoBehaviour
    {
        private static VoxelPool _instance;
        public static VoxelPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VoxelPool>();
                    if (_instance == null)
                    {
                        Debug.LogError("VoxelPool is missing from the scene! Please ensure a VoxelPool object exists.");
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private GameObject voxelPrefab;
        [SerializeField] private int initialSize = 250;
        
        private Queue<VoxelModel> pool = new Queue<VoxelModel>();

        // Initializes the voxel pool singleton and prewarms voxel instances.
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializePool();
        }

        // Validates the prefab reference and allocates the starting pool.
        private void InitializePool()
        {
            if (voxelPrefab == null)
            {
                Debug.LogError("VoxelPool: voxelPrefab is not assigned!");
                return;
            }

            for (int i = 0; i < initialSize; i++)
            {
                CreateNewVoxel();
            }
        }

        // Instantiates one pooled voxel and stores it as inactive.
        private void CreateNewVoxel()
        {
            GameObject go = Instantiate(voxelPrefab, transform);
            VoxelModel model = go.GetComponent<VoxelModel>();
            if (model == null)
            {
                Debug.LogError("VoxelPool: Voxel prefab is missing VoxelModel component!");
                return;
            }
            
            go.SetActive(false);
            pool.Enqueue(model);
        }

        // Fetches an active voxel instance from the pool.
        public VoxelModel Get()
        {
            if (pool.Count == 0)
            {
                CreateNewVoxel();
            }

            VoxelModel model = pool.Dequeue();
            model.gameObject.SetActive(true);
            return model;
        }

        // Returns a voxel instance to the pool and reparents it to the pool root.
        public void ReturnToPool(VoxelModel model)
        {
            if (model == null) return;
            
            model.gameObject.SetActive(false);
            model.transform.SetParent(transform);
            pool.Enqueue(model);
        }
    }
}
