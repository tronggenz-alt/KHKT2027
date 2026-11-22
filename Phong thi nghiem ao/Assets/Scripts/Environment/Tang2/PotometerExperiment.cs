using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Thí nghiệm đo tốc độ thoát hơi nước của cành cây bằng Potometer và giấy tẩm Coban Clorua (CoCl2):
    /// 1. Bọt khí trong ống mao dẫn dịch chuyển theo thể tích nước cành cây hút để bù vào lượng hơi nước thoát qua lá.
    /// 2. Giấy tẩm CoCl2 kẹp ở mặt dưới lá đổi màu xanh da trời -> hồng cánh sen rất nhanh do mật độ khí khổng cao gấp nhiều lần mặt trên.
    /// 3. Mặt trên có lớp cutin dày và ít khí khổng nên giấy đổi màu chậm hơn nhiều.
    /// </summary>
    public class PotometerExperiment : MonoBehaviour
    {
        [Header("Bọt khí trong ống mao dẫn")]
        public Transform bubbleTransform;
        public float startX = -0.2f;
        public float endX = 0.2f;
        public float travelSpeed = 0.008f; // m/s
        public float currentBubblePosMm = 0f;

        [Header("Giấy Coban Clorua (CoCl2)")]
        public Renderer cobaltPaperBottom; // Mặt dưới lá
        public Renderer cobaltPaperTop;    // Mặt trên lá
        public TextMesh statusText;

        static readonly Color DryCobaltBlue = new Color(0.25f, 0.45f, 0.88f);
        static readonly Color HydratedPink = new Color(0.95f, 0.45f, 0.65f);

        Material matBottomInstance;
        Material matTopInstance;
        float experimentTime = 0f;
        float currentX;
        float textTimer = 1f;
        bool finished;

        void Start()
        {
            if (cobaltPaperBottom != null) matBottomInstance = cobaltPaperBottom.material;
            if (cobaltPaperTop != null) matTopInstance = cobaltPaperTop.material;

            ResetExperiment();
        }

        void Update()
        {
            // Bọt đã tới cuối ống và cả hai mặt giấy đã đổi màu xong: không còn gì thay đổi, chờ người dùng đặt lại.
            if (finished) return;
            float dt = Time.deltaTime;
            experimentTime += dt;

            // 1. Dịch chuyển bọt khí. Tới cuối ống mao quản thì bọt dừng ở đó (như potometer thật, phải mở khóa bình
            //    chứa để đẩy bọt về). Bản cũ cho bọt nhảy về đầu ống trong khi đồng hồ vẫn chạy tiếp, nên số mm và
            //    số giây trên bảng không còn khớp nhau và không tính được tốc độ thoát hơi nước.
            currentX = Mathf.Min(endX, currentX + travelSpeed * dt);

            if (bubbleTransform != null)
            {
                Vector3 p = bubbleTransform.localPosition;
                p.x = currentX;
                bubbleTransform.localPosition = p;
            }

            currentBubblePosMm = Mathf.Clamp((currentX - startX) * 1000f, 0f, 400f);

            // 2. Chuyển màu giấy CoCl2: Mặt dưới (18s đạt 100%), Mặt trên (80s mới đạt)
            float tBottom = Mathf.Clamp01(experimentTime / 18f);
            float tTop = Mathf.Clamp01(experimentTime / 80f);

            if (matBottomInstance != null)
                matBottomInstance.color = Color.Lerp(DryCobaltBlue, HydratedPink, tBottom);

            if (matTopInstance != null)
                matTopInstance.color = Color.Lerp(DryCobaltBlue, HydratedPink, tTop);

            // 3. Cập nhật thông số khoa học: 4 lần/giây là đủ đọc. Bản cũ dựng chuỗi mới và dựng lại lưới chữ
            //    ở MỌI khung hình, kể cả khi người xem đang ở phòng khác.
            textTimer += dt;
            if (statusText != null && textTimer >= 0.25f)
            {
                textTimer = 0f;
                string bottomState = tBottom > 0.8f ? "<color=#ff77aa>HÓA HỒNG (Thoát mạnh)</color>" : "Đang hút ẩm...";
                string topState = tTop < 0.3f ? "<color=#4488ff>VẪN XANH (Thoát yếu)</color>" : "Chuyển màu chậm";

                statusText.text =
                    $"<b>THOÁT HƠI NƯỚC · POTOMETER</b>\n" +
                    $"Dịch chuyển bọt: {currentBubblePosMm:F0} mm ({experimentTime:F0}s)\n" +
                    $"Mặt dưới lá (Khí khổng nhiều): {bottomState}\n" +
                    $"Mặt trên lá (Ít khí khổng): {topState}\n" +
                    $"<i>[Chạm vào để đặt lại thí nghiệm]</i>";
                finished = currentX >= endX && experimentTime >= 80f;
            }
        }

        public void ResetExperiment()
        {
            experimentTime = 0f;
            currentX = startX;
            finished = false;
            textTimer = 1f;

            if (matBottomInstance != null) matBottomInstance.color = DryCobaltBlue;
            if (matTopInstance != null) matTopInstance.color = DryCobaltBlue;
        }

        void OnDestroy()
        {
            // .material ở Start tạo bản vật liệu riêng: phải tự hủy, nếu không mỗi lần nạp lại khu là rò thêm hai bản
            if (matBottomInstance != null) Destroy(matBottomInstance);
            if (matTopInstance != null) Destroy(matTopInstance);
        }

        void OnMouseDown()
        {
            ResetExperiment();
        }
    }
}

