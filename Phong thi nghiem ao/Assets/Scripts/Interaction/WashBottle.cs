using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>Bình tia nước cất: giữ để tia nước vào vật chứa (tráng thành bình, pha loãng).</summary>
    public class WashBottle : MonoBehaviour
    {
        public float rateMlPerS = 3f;
        public Transform nozzle;

        public void SprayInto(LabContainer target, float dt)
        {
            if (target != null) target.Add(Solution.Water(rateMlPerS * dt));
        }
    }
}