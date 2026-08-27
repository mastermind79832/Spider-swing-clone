using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class PlayerDemoRewards : MonoBehaviour
    {
        private int returnPoints;

        public int ReturnPoints => returnPoints;
        public event Action<int> OnPointsChanged;
        public event Action<int, Vector3> OnReturnRewardAwarded;

        public void AwardReturn(int value, Vector3 worldPosition)
        {
            var safeValue = Mathf.Max(0, value);
            returnPoints += safeValue;
            OnPointsChanged?.Invoke(returnPoints);
            OnReturnRewardAwarded?.Invoke(safeValue, worldPosition);
        }
    }

}
