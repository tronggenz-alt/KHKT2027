using Khkt.Demo;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>Hộp, giá cấp dụng cụ dùng nhiều (lam kính, ống nghiệm, cốc...): bấm là lấy một cái mới vào tay, làm bao nhiêu thí nghiệm cũng đủ.</summary>
    [DisallowMultipleComponent]
    public class BioDispenser : MonoBehaviour, IInteractable
    {
        public GameObject template;
        public string itemName = "dụng cụ";
        public int remaining = 50;
        HandController hand;
        int made;

        public string UseHint => remaining > 0 ? $"Lấy một {itemName} (còn {remaining})" : $"Đã hết {itemName}";

        public void Use(GameObject user)
        {
            if (hand == null) hand = FindAnyObjectByType<HandController>();
            if (hand != null && hand.Held != null) { BioFeedback.ShowToast("Đặt vật đang cầm xuống rồi mới lấy thêm", true); return; }
            Spawn(true);
        }

        /// <summary>Tạo một bản mới. toHand: đưa vào tay người xem.</summary>
        public GameObject Spawn(bool toHand)
        {
            if (template == null || remaining <= 0) { BioFeedback.ShowToast($"Đã hết {itemName}", true); return null; }
            remaining--;
            made++;
            var go = Instantiate(template, transform.parent);
            go.name = $"{template.name} {made}";
            go.transform.SetPositionAndRotation(transform.position + Vector3.up * 0.05f, transform.rotation);
            go.SetActive(true);
            BioFeedback.PlayClick();
            if (toHand)
            {
                if (hand == null) hand = FindAnyObjectByType<HandController>();
                var g = go.GetComponent<Grabbable>();
                if (hand != null && g != null) hand.Pick(g);
            }
            return go;
        }
    }
}
