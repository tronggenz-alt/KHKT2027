using System.Collections;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Thí nghiệm thử khí Oxy bằng que đóm tàn đỏ (SGK Sinh học 11):
    /// Khi đưa que đóm còn tàn đỏ lại gần miệng ống nghiệm thu khí quang hợp từ rong đuôi chó,
    /// lượng O2 đậm đặc làm que đóm bùng cháy thành ngọn lửa sáng chói.
    /// </summary>
    public class OxygenGlowingSplint : MonoBehaviour
    {
        public Transform testTubeMouth;
        public GameObject emberTip;
        public GameObject flameEffect;
        public Light flameLight;
        public TextMesh resultText;

        Vector3 initialPos;
        Quaternion initialRot;
        bool isTesting = false;

        void Start()
        {
            initialPos = transform.localPosition;
            initialRot = transform.localRotation;

            if (flameEffect != null) flameEffect.SetActive(false);
            if (flameLight != null) flameLight.enabled = false;
            if (resultText != null) resultText.text = "<b>Que đóm tàn đỏ</b>\n<i>[Chạm để đưa vào ống thu khí O₂]</i>";
        }

        void OnMouseDown()
        {
            if (!isTesting)
            {
                StartCoroutine(RunTestRoutine());
            }
        }

        IEnumerator RunTestRoutine()
        {
            isTesting = true;

            if (resultText != null)
                resultText.text = "<b>Đang đưa que đóm vào ống thu khí...</b>";

            Vector3 targetPos = testTubeMouth != null ? testTubeMouth.position : transform.position + Vector3.up * 0.15f;
            Quaternion targetRot = Quaternion.Euler(0, 0, -45f);

            // 1. Di chuyển que đóm lại gần miệng ống nghiệm
            float elapsed = 0f;
            float duration = 1.0f;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            // 2. Bùng cháy trong môi trường giàu O2!
            if (flameEffect != null) flameEffect.SetActive(true);
            if (flameLight != null) flameLight.enabled = true;
            if (emberTip != null) emberTip.SetActive(false);

            if (resultText != null)
            {
                resultText.text =
                    "<color=#ffdd44><b>🔥 QUE ĐÓM BÙNG CHÁY MÃNH LIỆT!</b></color>\n" +
                    "Chứng minh khí thu được là <b>Oxy (O₂)</b>\n" +
                    "sinh ra từ quá trình quang hợp của rong!";
            }

            // Giữ ngọn lửa cháy 3 giây để người học quan sát
            yield return new WaitForSeconds(3.0f);

            // 3. Tắt lửa và đưa que đóm về vị trí ban đầu
            if (flameEffect != null) flameEffect.SetActive(false);
            if (flameLight != null) flameLight.enabled = false;
            if (emberTip != null) emberTip.SetActive(true);

            elapsed = 0f;
            startPos = transform.position;
            startRot = transform.rotation;
            Vector3 returnPos = transform.parent != null ? transform.parent.TransformPoint(initialPos) : initialPos;
            Quaternion returnRot = transform.parent != null ? transform.parent.rotation * initialRot : initialRot;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, returnPos, t);
                transform.rotation = Quaternion.Slerp(startRot, returnRot, t);
                yield return null;
            }

            transform.localPosition = initialPos;
            transform.localRotation = initialRot;

            if (resultText != null)
                resultText.text = "<b>Que đóm tàn đỏ</b>\n<i>[Chạm để đưa vào ống thu khí O₂]</i>";

            isTesting = false;
        }
    }
}

