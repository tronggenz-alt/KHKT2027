using Khkt.Environment;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bật/tắt một hiệu ứng khi bấm vào: ngọn lửa đèn cồn, dòng nước ở vòi, đèn trần, quạt trần...
    /// </summary>
    public class ToggleEffect : MonoBehaviour, IDemoUsable
    {
        public bool isOn;
        [Tooltip("Hiện khi bật (ngọn lửa, dòng nước, bóng đèn sáng)")]
        public GameObject[] showWhenOn;
        [Tooltip("Ẩn khi bật (nắp đèn cồn)")]
        public GameObject[] hideWhenOn;
        public Light[] lights;
        public CeilingFan[] fans;
        public string onHint = "Bấm chuột trái để bật";
        public string offHint = "Bấm chuột trái để tắt";

        public string UseHint => isOn ? offHint : onHint;

        void Start() => Apply();

        public void Use()
        {
            isOn = !isOn;
            Apply();
        }

        void Apply()
        {
            if (showWhenOn != null)
                foreach (var g in showWhenOn) if (g != null) g.SetActive(isOn);
            if (hideWhenOn != null)
                foreach (var g in hideWhenOn) if (g != null) g.SetActive(!isOn);
            if (lights != null)
                foreach (var l in lights) if (l != null) l.enabled = isOn;
            if (fans != null)
                foreach (var f in fans) if (f != null) f.running = isOn;
        }
    }
}
