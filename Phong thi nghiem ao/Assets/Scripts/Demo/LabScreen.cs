using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Màn hình trong phòng (tường video, màn hình nhóm ở bàn, bảng tin hành lang) để chiếu bài và thảo luận.
    /// Nguồn nội dung:
    ///   1. Ảnh slide đặt trong StreamingAssets/BaiGiang/&lt;slideFolder&gt;/ (png, jpg, xếp theo tên file): xuất bài giảng
    ///      PowerPoint ra ảnh rồi thả vào đó, mở game là có (slide đứng trước các trang có sẵn).
    ///   2. Trang có sẵn do trình dựng cảnh soạn: tiêu đề + nội dung, có thể kèm ảnh nền.
    ///   3. Camera trực tiếp (ví dụ camera trên bàn giáo viên): chỉ chụp liveFps lần/giây và chỉ khi màn hình đang được nhìn thấy.
    ///   4. Trang thảo luận do chương trình khác đẩy vào (ShowText, ShowImage) - chỗ để web quản lý đẩy nội dung sau này.
    /// Bấm vào màn hình (IDemoUsable) để sang trang; không ai bấm thì tự chuyển sau autoAdvanceS giây.
    /// </summary>
    public class LabScreen : MonoBehaviour, IDemoUsable
    {
        [System.Serializable]
        public class Page
        {
            public string title;
            [TextArea] public string body;
            public Texture2D image;
            public bool live;
        }

        public string screenId = "man_hinh";
        [Tooltip("Thư mục con trong StreamingAssets/BaiGiang chứa ảnh slide cho màn hình này")]
        public string slideFolder = "";
        [Tooltip("Mặt màn hình (vật liệu phát sáng); ảnh trang hiện tại gán vào ảnh nền và ảnh phát sáng")]
        public Renderer surface;
        public TextMesh titleText, bodyText;
        public Texture2D background;
        public Camera liveCamera;
        public Vector2Int liveResolution = new Vector2Int(640, 360);
        public float liveFps = 8f;
        [Tooltip("Tự sang trang sau chừng này giây (0 = không tự chuyển)")]
        public float autoAdvanceS = 15f;
        public List<Page> pages = new List<Page>();

        static readonly Dictionary<string, LabScreen> registry = new Dictionary<string, LabScreen>();
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), EmissionMapId = Shader.PropertyToID("_EmissionMap");

        /// <summary>Màn hình theo mã (ví dụ "tuong_video_2", "ban_1", "hanh_lang").</summary>
        public static LabScreen Find(string id) => id != null && registry.TryGetValue(id, out var s) ? s : null;
        public static IEnumerable<LabScreen> All => registry.Values;

        public int PageIndex { get; private set; }
        public Page Current => pages.Count > 0 ? pages[PageIndex] : null;
        public string UseHint => "Màn hình: trang sau";

        Material mat;
        RenderTexture liveRt;
        float liveTimer, autoTimer, pauseAuto;
        readonly List<Texture2D> loadedSlides = new List<Texture2D>();

        void Awake()
        {
            registry[screenId] = this;
            if (surface != null) mat = surface.material;  // bản riêng của màn hình này
            LoadSlides();
        }

        void Start() => Show(0);

        void OnDestroy()
        {
            if (registry.TryGetValue(screenId, out var s) && s == this) registry.Remove(screenId);
            if (liveRt != null) liveRt.Release();
            foreach (var t in loadedSlides) if (t != null) Destroy(t);
            if (mat != null) Destroy(mat);
        }

        void LoadSlides()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "BaiGiang", slideFolder ?? "");
            if (!Directory.Exists(dir)) return;
            var files = new List<string>();
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") files.Add(f);
            }
            files.Sort(System.StringComparer.OrdinalIgnoreCase);
            int at = 0;
            foreach (var f in files)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileName(f) };
                if (!tex.LoadImage(File.ReadAllBytes(f))) { Destroy(tex); continue; }
                loadedSlides.Add(tex);
                pages.Insert(at++, new Page { image = tex });
            }
        }

        public void Use()
        {
            pauseAuto = 60f;  // người xem tự chuyển trang: thôi tự chuyển 1 phút
            Show(PageIndex + 1);
        }

        public void Show(int index)
        {
            if (pages.Count == 0) return;
            PageIndex = ((index % pages.Count) + pages.Count) % pages.Count;
            autoTimer = 0;
            var p = pages[PageIndex];
            Texture tex = p.live ? EnsureLive() : p.image != null ? p.image : background;
            if (mat != null)
            {
                mat.SetTexture(BaseMapId, tex);
                mat.SetTexture(EmissionMapId, tex);
            }
            bool imageOnly = !p.live && p.image != null && string.IsNullOrEmpty(p.title) && string.IsNullOrEmpty(p.body);
            if (titleText != null)
            {
                titleText.text = p.title ?? "";
                titleText.gameObject.SetActive(!imageOnly && !string.IsNullOrEmpty(p.title));
            }
            if (bodyText != null)
            {
                bodyText.text = p.body ?? "";
                bodyText.gameObject.SetActive(!imageOnly && !p.live && !string.IsNullOrEmpty(p.body));
            }
            if (p.live) RenderLive();
        }

        /// <summary>Đẩy một trang chữ (câu hỏi thảo luận, ghi chú) lên màn hình và chuyển tới trang đó.</summary>
        public void ShowText(string title, string body) => Push(new Page { title = title, body = body });

        public void ShowImage(Texture2D image, string caption = "") => Push(new Page { image = image, title = caption });

        public void ShowLive(Camera cam, string caption = "")
        {
            liveCamera = cam;
            Push(new Page { live = true, title = caption });
        }

        void Push(Page p)
        {
            pages.Add(p);
            pauseAuto = 60f;
            Show(pages.Count - 1);
        }

        RenderTexture EnsureLive()
        {
            if (liveCamera == null) return null;
            if (liveRt == null)
            {
                liveRt = new RenderTexture(liveResolution.x, liveResolution.y, 16) { name = "Camera " + screenId };
                liveCamera.targetTexture = liveRt;
                liveCamera.enabled = false;  // chỉ vẽ khi cần, không vẽ mỗi khung hình
            }
            return liveRt;
        }

        void RenderLive()
        {
            if (liveCamera != null && EnsureLive() != null) liveCamera.Render();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var p = Current;
            if (p != null && p.live && surface != null && surface.isVisible)
            {
                liveTimer += dt;
                if (liveTimer >= 1f / Mathf.Max(1f, liveFps))
                {
                    liveTimer = 0;
                    RenderLive();
                }
            }
            if (pauseAuto > 0) { pauseAuto -= dt; return; }
            if (autoAdvanceS > 0 && pages.Count > 1)
            {
                autoTimer += dt;
                if (autoTimer >= autoAdvanceS) Show(PageIndex + 1);
            }
        }
    }
}
