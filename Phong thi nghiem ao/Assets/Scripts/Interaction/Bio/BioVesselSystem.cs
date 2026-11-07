using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Chạy luật cho mọi bình đang thức, rải đều 5 lần/giây mỗi bình: không phải mỗi bình một Update mỗi khung hình.</summary>
    public class BioVesselSystem : MonoBehaviour
    {
        static readonly List<BioVessel> vessels = new List<BioVessel>();
        static BioVesselSystem instance;
        int cursor;
        float budget;

        public static void Register(BioVessel v)
        {
            if (!vessels.Contains(v)) vessels.Add(v);
            if (instance == null)
            {
                var go = new GameObject("Hệ bình sinh học");
                instance = go.AddComponent<BioVesselSystem>();
                go.AddComponent<BioLabZones>();
            }
        }

        public static void Unregister(BioVessel v) => vessels.Remove(v);

        void Update()
        {
            int n = vessels.Count;
            if (n == 0) return;
            budget += n * 5f * Time.deltaTime;
            int run = Mathf.Min(n, (int)budget);
            budget -= run;
            for (int i = 0; i < run; i++)
            {
                cursor = (cursor + 1) % vessels.Count;
                var v = vessels[cursor];
                if (v != null) v.Tick();
            }
        }
    }
}
