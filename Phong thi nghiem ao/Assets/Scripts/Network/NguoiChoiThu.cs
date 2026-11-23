using Khkt.Demo;
using Khkt.Environment;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Network
{
    /// <summary>
    /// Chế độ thử thân người 3D khi chỉ có một máy: phím Y trong phòng thí nghiệm (NguoiChoiThuPhim) hoặc menu KHKT trong Editor.
    /// Dựng thân người ngay chỗ người chơi, lái bằng chính đầu, bàn tay ảo, vật đang cầm của mình giống hệt cách
    /// LabPresence lái người cùng phòng, rồi mở khung camera góc nhìn thứ ba ở góc màn hình: thấy mình trong mắt người khác.
    /// Camera chính không vẽ thân người này (lớp riêng) nên góc nhìn hai tay và mọi thao tác giữ nguyên.
    /// </summary>
    [DefaultExecutionOrder(100)]   // sau DesktopViewer, HandController: dùng vị trí đầu, tay của khung này
    public class NguoiChoiThu : MonoBehaviour
    {
        /// <summary>Lớp chỉ camera góc nhìn thứ ba vẽ (lớp 30 đã dùng cho khung xem trước dụng cụ).</summary>
        const int Layer = 29;

        RemoteAvatar avatar;
        DesktopViewer viewer;
        HandController hand;
        Camera mainCam, cam;
        RenderTexture rt;
        int oldMask;
        Vector3 camPos;
        bool camPlaced;

        public static bool Active => FindAnyObjectByType<NguoiChoiThu>() != null;

        /// <summary>Bật / tắt chế độ thử.</summary>
        public static void Toggle()
        {
            var cur = FindAnyObjectByType<NguoiChoiThu>();
            if (cur != null) Destroy(cur.gameObject);
            else new GameObject("Thử thân người 3D").AddComponent<NguoiChoiThu>();
        }

        void Start()
        {
            viewer = FindAnyObjectByType<DesktopViewer>();
            hand = HandController.Active != null ? HandController.Active : FindAnyObjectByType<HandController>();
            if (viewer == null || viewer.cameraTransform == null)
            {
                LabMessages.Say("Vào phòng thí nghiệm rồi mới thử thân người 3D", LabMessages.Tone.Warn);
                Destroy(gameObject);
                return;
            }
            avatar = RemoteAvatar.Create(transform, "Thân người (thử)");
            if (avatar == null)
            {
                LabMessages.Say("Chưa có file nhân vật: xem Assets/Lab/NguoiChoi/DOC_TRUOC.txt", LabMessages.Tone.Warn);
                Destroy(gameObject);
                return;
            }
            foreach (var t in avatar.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            mainCam = viewer.viewCamera;
            oldMask = mainCam != null ? mainCam.cullingMask : ~0;
            if (mainCam != null) mainCam.cullingMask = oldMask & ~(1 << Layer);

            rt = new RenderTexture(960, 600, 24) { name = "Góc nhìn thứ ba" };
            var cg = new GameObject("Camera góc nhìn thứ ba");
            cg.transform.SetParent(transform, false);
            cam = cg.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.cullingMask = oldMask | (1 << Layer);
            BuildFrame();
            LabMessages.Say("Khung góc phải: người khác nhìn thấy bạn thế này. Bấm Y lần nữa để tắt", LabMessages.Tone.Info);
        }

        void BuildFrame()
        {
            var canvasGo = new GameObject("Khung góc nhìn thứ ba", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var frame = GlassKit.Rect(canvasGo.transform, "Viền", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(560, 400));
            var bg = frame.gameObject.AddComponent<Image>();
            bg.sprite = GlassKit.Solid;
            bg.color = new Color(0.03f, 0.05f, 0.09f, 0.86f);
            bg.raycastTarget = false;
            var view = GlassKit.Rect(frame, "Hình", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(548, 342));
            var raw = view.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            GlassKit.Label(frame, "Chữ", "Người khác nhìn thấy bạn", 20, Color.white, true, TextAnchor.MiddleLeft, new Vector2(14, -4), new Vector2(532, 44));
        }

        void Update()
        {
            if (avatar == null || viewer == null || viewer.cameraTransform == null) return;
            // giống LabPresence.SendPose: mắt, hướng nhìn, găng tay của chuột, vật đang cầm
            var head = viewer.cameraTransform;
            bool hasGlove = hand != null && hand.hand != null;
            Vector3 handPos = hasGlove ? hand.hand.transform.position : head.position + head.forward * 0.4f;
            Quaternion handRot = hasGlove ? hand.hand.transform.rotation : head.rotation;
            var look = Quaternion.Euler(viewer.Pitch, viewer.transform.eulerAngles.y, 0);
            avatar.Drive(head.position, look, handPos, handRot, hand != null && hand.Held != null);
        }

        void LateUpdate()
        {
            if (cam == null || viewer == null || viewer.cameraTransform == null) return;
            // camera sau lưng, chếch vai phải, kiểu game nhập vai; không xuyên tường
            var head = viewer.cameraTransform.position;
            Vector3 fwd = viewer.transform.forward;
            fwd.y = 0;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 want = head - fwd * 2.3f + Vector3.up * 0.35f + right * 0.55f;
            Vector3 dir = want - head;
            if (Physics.SphereCast(head, 0.12f, dir.normalized, out var hit, dir.magnitude, ~(1 << Layer), QueryTriggerInteraction.Ignore))
                want = head + dir.normalized * Mathf.Max(0.3f, hit.distance - 0.05f);
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            camPos = camPlaced ? Vector3.Lerp(camPos, want, k) : want;
            camPlaced = true;
            cam.transform.position = camPos;
            cam.transform.LookAt(head - Vector3.up * 0.45f + fwd * 0.5f);
        }

        void OnDestroy()
        {
            if (mainCam != null) mainCam.cullingMask = oldMask;
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }
    }
}
