using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Thẻ chú thích 3D nổi dạng viên thuốc bo tròn (Floating 3D Pill Callouts):
    ///   • Tự động hiện phía trên các máy phân tích & thiết bị thí nghiệm khi camera lại gần (< 2.6m).
    ///   • Nền kính mờ bo tròn chuẩn UI hiện đại (rgba(10, 16, 26, 0.78)) kèm viền sáng thanh mảnh.
    ///   • Chấm trạng thái nhỏ (xanh ngọc khi hoạt động, vàng khi cần chú ý).
    ///   • Billboard Y thông minh: xoay theo trục Y hướng về người dùng, không bị lộn ngược khi nhìn từ trên cao.
    ///   • Hiển thị thông số thời gian thực (Độ phóng đại kính hiển vi, pH, khối lượng cân, nhiệt độ bể...).
    /// </summary>
    public class FloatingCalloutGroup : MonoBehaviour
    {
        public static FloatingCalloutGroup Instance { get; private set; }

        public float activationDistance = 2.6f;
        readonly List<PillCallout> callouts = new List<PillCallout>();
        Transform camTransform;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            FindCamera();
            AutoDiscoverEquipment();
        }

        void FindCamera()
        {
            var cam = Camera.main;
            if (cam != null) camTransform = cam.transform;
            else
            {
                var v = FindAnyObjectByType<Khkt.Environment.DesktopViewer>();
                if (v != null && v.viewCamera != null) camTransform = v.viewCamera.transform;
            }
        }

        public void Register(PillCallout pill)
        {
            if (pill != null && !callouts.Contains(pill))
                callouts.Add(pill);
        }

        public void Unregister(PillCallout pill)
        {
            if (pill != null) callouts.Remove(pill);
        }

        void AutoDiscoverEquipment()
        {
            // Tự động tìm các máy móc lớn chưa gắn PillCallout
            var infos = FindObjectsByType<EquipmentInfo>(FindObjectsSortMode.None);
            foreach (var info in infos)
            {
                if (info.GetComponentInChildren<PillCallout>() != null) continue;
                // Chỉ gắn cho thiết bị/máy móc chính
                string n = info.gameObject.name.ToLower();
                bool isKeyMachine = n.Contains("kính hiển vi") || n.Contains("máy") || n.Contains("cân") ||
                                    n.Contains("nồi hấp") || n.Contains("bể") || n.Contains("ly tâm") ||
                                    n.Contains("vortex") || n.Contains("pcr") || n.Contains("quang phổ") ||
                                    n.Contains("điện di") || n.Contains("thủy canh");
                if (isKeyMachine)
                {
                    AttachPill(info.transform, info.title, Vector3.up * 0.42f);
                }
            }
        }

        public static PillCallout AttachPill(Transform target, string title, Vector3 localOffset, string status = "")
        {
            var go = new GameObject("PillCallout", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(target, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = Vector3.one * 0.001f;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300, 60);

            var pill = go.AddComponent<PillCallout>();
            pill.title = title;
            pill.statusText = status;
            pill.BuildVisuals();

            if (Instance != null) Instance.Register(pill);
            return pill;
        }

        void Update()
        {
            if (camTransform == null)
            {
                FindCamera();
                if (camTransform == null) return;
            }

            Vector3 camPos = camTransform.position;
            for (int i = 0; i < callouts.Count; i++)
            {
                var pill = callouts[i];
                if (pill == null) continue;
                pill.UpdateCallout(camPos, activationDistance);
            }
        }
    }

    /// <summary>
    /// Thực thể viên thuốc 3D nổi cho từng thiết bị.
    /// </summary>
    public class PillCallout : MonoBehaviour
    {
        public string title = "";
        public string statusText = "";

        Color customDotColor;
        bool hasCustomDot;
        public Color dotColor
        {
            get => hasCustomDot ? customDotColor : Good;
            set
            {
                customDotColor = value;
                hasCustomDot = true;
                if (dotImg != null) dotImg.color = value;
            }
        }

        CanvasGroup canvasGroup;
        Image bgImg, borderImg, dotImg;
        Text titleLabel, statusLabel;
        float currentAlpha;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            UiTheme.Changed += ApplyTheme;
        }

        void OnDestroy()
        {
            UiTheme.Changed -= ApplyTheme;
        }

        public void BuildVisuals()
        {
            float cs = UiTheme.IsVera ? 1.8f : 1f;

            // Viền thanh mảnh ngoài cùng (Solid) + nền nội thất lùi vào 1.5px để không đè chữ
            borderImg = gameObject.AddComponent<Image>();
            borderImg.sprite = Solid;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = cs;
            borderImg.color = Line;
            borderImg.raycastTarget = false;

            var bgRt = Rect(transform, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.offsetMin = new Vector2(1.5f, 1.5f);
            bgRt.offsetMax = new Vector2(-1.5f, -1.5f);
            bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = Solid;
            bgImg.type = Image.Type.Sliced;
            bgImg.pixelsPerUnitMultiplier = cs;
            bgImg.color = SurfaceA(0.92f);
            bgImg.raycastTarget = false;

            // Chấm trạng thái nhỏ (xanh ngọc khi hoạt động)
            var dotRt = Rect(transform, "Dot", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(10, 10));
            dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = SolidCircle;
            dotImg.color = dotColor;
            dotImg.raycastTarget = false;

            // Nhãn tiêu đề thiết bị
            titleLabel = Label(transform, "Title", title, 17, Ink, true, TextAnchor.MiddleLeft, new Vector2(38, 12), new Vector2(250, 24), new Vector2(0, 0.5f));
            titleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Nhãn thông số / trạng thái
            string sub = string.IsNullOrEmpty(statusText) ? "Sẵn sàng vận hành" : statusText;
            statusLabel = Label(transform, "Status", sub, 12, Accent, false, TextAnchor.MiddleLeft, new Vector2(38, -12), new Vector2(250, 18), new Vector2(0, 0.5f));
            statusLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        void ApplyTheme()
        {
            float cs = UiTheme.IsVera ? 1.8f : 1f;
            if (borderImg != null) { borderImg.sprite = Solid; borderImg.pixelsPerUnitMultiplier = cs; borderImg.color = Line; }
            if (bgImg != null) { bgImg.sprite = Solid; bgImg.pixelsPerUnitMultiplier = cs; bgImg.color = SurfaceA(0.92f); }
            if (dotImg != null && !hasCustomDot) dotImg.color = Good;
            if (titleLabel != null) titleLabel.color = Ink;
            if (statusLabel != null) statusLabel.color = Accent;
        }

        public void SetStatus(string text, Color? color = null)
        {
            if (statusLabel != null) statusLabel.text = text;
            if (color.HasValue) dotColor = color.Value;
        }

        public void UpdateCallout(Vector3 camPos, float maxDist)
        {
            float dist = Vector3.Distance(camPos, transform.position);
            float targetAlpha = dist < maxDist ? Mathf.Clamp01((maxDist - dist) / 0.6f) : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * 4f);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = currentAlpha;
            }

            if (currentAlpha > 0.01f)
            {
                // Billboard theo trục Y: quay ngang hướng về camera, giữ trục đứng không bị lộn ngược
                Vector3 toCam = camPos - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(toCam);
                }
            }
        }
    }
}

