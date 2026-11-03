using Khkt.Demo.Bio;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Gắn vùng nhiệt vào các máy đã có trong tòa nhà: nhiệt độ đọc trực tiếp từ máy (vặn núm là bình đổi theo).</summary>
    public class BioLabZones : MonoBehaviour
    {
        void Start()
        {
            foreach (var b in FindObjectsByType<WaterBathController>(FindObjectsInactive.Include))
                Add(b.gameObject, () => b.targetTempC, 0.45f, 0.15f, 60, "Bể ổn nhiệt");
            foreach (var i in FindObjectsByType<IncubatorController>(FindObjectsInactive.Include))
                Add(i.gameObject, () => i.targetTempC, 0.7f, 0.12f, 1800, "Tủ ấm");  // 1 giây thật = 30 phút: nuôi cấy qua đêm xem được trong buổi học
            foreach (var f in FindObjectsByType<FridgeController>(FindObjectsInactive.Include))
                Add(f.gameObject, () => f.setTempC, 0.8f, 0.1f, 60, f.isDeepFreezer ? "Tủ đông" : "Tủ lạnh");
            foreach (var m in FindObjectsByType<MicrowaveOven>(FindObjectsInactive.Include))
                Add(m.gameObject, () => 100f, 0.3f, 0.5f, 60, "Lò vi sóng");
            foreach (var l in FindObjectsByType<SpiritLamp>(FindObjectsInactive.Include))
            {
                var z = Add(l.gameObject, () => 100f, 0.13f, 0.35f, 60, "Đèn cồn");
                z.isOn = () => l.isLit;
            }
            foreach (var w in FindObjectsByType<HandWashStation>(FindObjectsInactive.Include))
                if (w.GetComponent<BioDrain>() == null) w.gameObject.AddComponent<BioDrain>();
        }

        static BioThermalZone Add(GameObject go, System.Func<float> t, float radius, float rate, double scale, string name)
        {
            var z = go.GetComponent<BioThermalZone>();
            if (z == null) z = go.AddComponent<BioThermalZone>();
            z.temperature = t;
            z.radiusM = radius;
            z.rate = rate;
            z.timeScale = scale;
            z.zoneName = name;
            return z;
        }
    }
}
