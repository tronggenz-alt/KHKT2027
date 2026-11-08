using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Máy khuấy từ gia nhiệt: bấm để bật, tắt, lăn chuột để đặt nhiệt độ; bình đặt lên nóng dần.</summary>
    [DisallowMultipleComponent]
    public class BioHotPlate : MonoBehaviour, IInteractable, IScrollAdjustable
    {
        public float setTempC = 60f;
        public bool on;

        public string UseHint => $"Máy khuấy từ gia nhiệt [{(on ? $"đang bật, {setTempC:0} °C" : "đang tắt")}] · Bấm để bật, tắt, lăn chuột đặt nhiệt độ";

        void Awake()
        {
            var z = gameObject.AddComponent<BioThermalZone>();
            z.radiusM = 0.12f;
            z.rate = 0.2f;
            z.zoneName = "Máy khuấy từ";
            z.temperature = () => setTempC;
            z.isOn = () => on;
        }

        public void Use(GameObject user)
        {
            on = !on;
            BioFeedback.PlayClick();
            BioFeedback.ShowToast(on ? $"Máy khuấy từ bật ở {setTempC:0} °C: đặt bình lên mặt bếp" : "Máy khuấy từ đã tắt");
        }

        public void OnScroll(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) return;
            setTempC = Mathf.Clamp(setTempC + Mathf.Sign(delta) * 5f, 25f, 200f);
            BioFeedback.PlayClick();
        }
    }
}
