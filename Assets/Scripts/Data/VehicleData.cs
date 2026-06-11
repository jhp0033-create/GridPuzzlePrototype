using UnityEngine;

namespace GridPuzzle.Data
{
    [System.Serializable]
    public class VehicleData
    {
        public string instanceID;
        public ColorType colorType;
        public int maxQuota;
        public int currentQuota;
        public VehicleState state;

        // Creates one vehicle payload with matching max and current quota values.
        public VehicleData(string id, ColorType color, int quota)
        {
            instanceID = id;
            colorType = color;
            maxQuota = quota;
            currentQuota = quota;
            state = VehicleState.InQueue;
        }
    }
}
