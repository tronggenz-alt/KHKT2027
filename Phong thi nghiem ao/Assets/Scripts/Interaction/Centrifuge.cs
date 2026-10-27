using Khkt.Demo;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Máy ly tâm: đặt ống nghiệm vào ổ, bấm máy để quay vài giây; kết tủa lắng hết xuống đáy,
    /// phần dung dịch phía trên trong lại (tách rắn - lỏng).
    /// </summary>
    public class Centrifuge : MonoBehaviour, IDemoUsable
    {
        public PlaceSocket[] slots;
        public Transform rotor;
        public float spinSeconds = 4f;
        public float rpm = 3000f;

        float spinLeft;

        public bool Spinning => spinLeft > 0;
        public string UseHint => Spinning ? "Đang quay..." : "Bấm chuột trái: ly tâm (tách kết tủa)";

        public void Use()
        {
            if (!Spinning) spinLeft = spinSeconds;
        }

        void Update()
        {
            if (spinLeft <= 0) return;
            spinLeft -= Time.deltaTime;
            float speed = Mathf.Clamp01(Mathf.Min(spinSeconds - spinLeft, spinLeft) / 0.8f);
            if (rotor != null) rotor.Rotate(0, rpm / 60f * 360f * 0.05f * speed * Time.deltaTime, 0, Space.Self);
            if (slots == null) return;
            foreach (var s in slots)
            {
                var c = s != null && s.Occupant != null ? s.Occupant.GetComponent<LabContainer>() : null;
                if (c != null) c.ForceSettle();
            }
        }
    }
}
