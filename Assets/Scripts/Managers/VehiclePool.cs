using UnityEngine;
using System.Collections.Generic;
using GridPuzzle.Core;

namespace GridPuzzle.Managers
{
    public class VehiclePool : MonoBehaviour
    {
        public static VehiclePool Instance { get; private set; }

        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField] private int initialSize = 20;
        
        private Queue<VehicleModel> pool = new Queue<VehicleModel>();

        // Initializes the vehicle pool singleton and prewarms instances.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializePool();
        }

        // Pre-creates the initial set of pooled vehicle objects.
        private void InitializePool()
        {
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewVehicle();
            }
        }

        // Instantiates one pooled vehicle and registers it as inactive.
        private void CreateNewVehicle()
        {
            GameObject go = Instantiate(vehiclePrefab, transform);
            VehicleModel model = go.GetComponent<VehicleModel>();
            if (model == null)
            {
                Debug.LogError("VehiclePool: Vehicle prefab is missing VehicleModel component!");
                return;
            }
            
            go.SetActive(false);
            pool.Enqueue(model);
        }

        // Fetches an active vehicle instance from the pool.
        public VehicleModel Get()
        {
            if (pool.Count == 0)
            {
                CreateNewVehicle();
            }
            
            VehicleModel model = pool.Dequeue();
            model.gameObject.SetActive(true);
            return model;
        }

        // Returns a vehicle instance to the inactive pool root.
        public void ReturnToPool(VehicleModel model)
        {
            model.gameObject.SetActive(false);
            model.transform.SetParent(transform);
            pool.Enqueue(model);
        }
    }
}
