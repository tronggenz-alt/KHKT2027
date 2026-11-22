using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Demo;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// Thẻ kiến thức giải phẫu và sinh lý 15 cơ quan (GĐ4):
    ///   • Đọc dữ liệu từ Application.streamingAssetsPath/data/sinh/kien_thuc/{id}.json.
    ///   • Tự động nạp 1 lần vào bộ nhớ đệm (cache), không I/O đĩa trong lúc tương tác hay chuyển cảnh.
    ///   • uGUI kính mờ GlassKit: Tiêu đề, tên khoa học, vị trí, cấu tạo, chức năng, cơ chế, liên hệ thực tế, điểm thi.
    ///   • Định dạng số thập phân dùng dấu phẩy (CultureInfo("vi-VN")).
    ///   • Hộp thoại ôn tập trắc nghiệm (trac_nghiem): kiểm tra đúng/sai ngay, giải thích chi tiết, đếm điểm.
    ///   • Nút soi hiển vi liên kết trực tiếp với MicroSceneViewer.
    /// </summary>
    public class OrganKnowledgeCard : MonoBehaviour
    {
        [Serializable]
        public class TracNghiemItem
        {
            public string cau;
            public string[] lua_chon;
            public string dap_an;
            public string giai_thich;
        }

        [Serializable]
        public class OrganData
        {
            public string id;
            public string ten;
            public string chuong_trinh;
            public string[] vi_tri;
            public string[] cau_tao;
            public string[] chuc_nang;
            public string[] co_che;
            public string[] lien_he;
            public string[] diem_thi;
            public TracNghiemItem[] trac_nghiem;
        }

        [Serializable]
        public class DanhMucItem
        {
            public string id;
            public string ten;
            public string icon;
            public string he;
            public string tep;
            public string[] bo_phan;
        }

        [Serializable]
        public class DanhMucRoot
        {
            public int phien_ban;
            public string ghi_chu;
            public DanhMucItem[] co_quan;
        }

        // Cache toàn bộ dữ liệu 15 cơ quan trong RAM
        private static readonly Dictionary<string, OrganData> DataCache = new Dictionary<string, OrganData>();
        private static bool isCacheLoaded = false;
        private static DanhMucItem[] danhMucItems = null;

        [Header("Giao diện Thẻ kiến thức")]
        public RectTransform cardPanel;
        public CanvasGroup cardCanvasGroup;
        public Text titleText;
        public Text programText;
        public Text contentBodyText;
        public ScrollRect contentScroll;
        public Button closeButton;
        public Button quizButton;
        public Button microButton;

        [Header("Hộp thoại Ôn tập Trắc nghiệm")]
        public RectTransform quizPanel;
        public CanvasGroup quizCanvasGroup;
        public Text quizTitleText;
        public Text quizQuestionText;
        public Text quizProgressText;
        public Text quizExplanationText;
        public Button[] optionButtons = new Button[4];
        public Text[] optionTexts = new Text[4];
        public Image[] optionBackgrounds = new Image[4];
        public Button nextQuizButton;
        public Button prevQuizButton;
        public Button closeQuizButton;

        private OrganData currentOrgan;
        private int currentQuizIndex = 0;
        private int correctAnswersCount = 0;
        private bool hasAnsweredCurrentQuestion = false;

        public Action<string> OnRequestMicroScene;
        public Action OnClosed;

        private static readonly CultureInfo ViCulture = new CultureInfo("vi-VN");

        public static bool IsCacheReady => isCacheLoaded;
        public OrganData CurrentOrgan => currentOrgan;

        /// <summary>
        /// Nạp trước toàn bộ dữ liệu 15 file JSON từ StreamingAssets vào bộ nhớ đệm (gọi khi vào phòng ảo).
        /// </summary>
        public static void PreloadAllData()
        {
            if (isCacheLoaded) return;

            string basePath = Path.Combine(Application.streamingAssetsPath, "data", "sinh", "kien_thuc");
            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"[OrganKnowledgeCard] Không tìm thấy thư mục kiến thức: {basePath}");
                return;
            }

            // Đọc danh mục
            string danhMucPath = Path.Combine(basePath, "danh-muc.json");
            if (File.Exists(danhMucPath))
            {
                try
                {
                    string json = File.ReadAllText(danhMucPath);
                    var root = JsonUtility.FromJson<DanhMucRoot>(json);
                    if (root != null && root.co_quan != null)
                    {
                        danhMucItems = root.co_quan;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[OrganKnowledgeCard] Lỗi đọc danh-muc.json: {ex.Message}");
                }
            }

            // Danh sách file cơ quan chuẩn
            string[] fileNames = new string[]
            {
                "tim.json", "phoi.json", "nao.json", "gan.json", "than.json",
                "da-day.json", "ruot-non.json", "ruot-gia.json", "co.json", "xuong.json",
                "da.json", "mach-mau.json", "tuy-song.json", "lach.json", "tuy.json"
            };

            foreach (var fn in fileNames)
            {
                string fp = Path.Combine(basePath, fn);
                if (File.Exists(fp))
                {
                    try
                    {
                        string json = File.ReadAllText(fp);
                        var organ = JsonUtility.FromJson<OrganData>(json);
                        if (organ != null && !string.IsNullOrEmpty(organ.id))
                        {
                            DataCache[organ.id] = organ;
                            string altId = organ.id.Replace('_', '-');
                            DataCache[altId] = organ;
                            string baseId = Path.GetFileNameWithoutExtension(fn);
                            DataCache[baseId] = organ;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[OrganKnowledgeCard] Lỗi đọc tệp '{fn}': {ex.Message}");
                    }
                }
            }

            isCacheLoaded = true;
            Debug.Log($"[OrganKnowledgeCard] Đã nạp thành công {DataCache.Count / 2} cơ quan vào bộ nhớ đệm.");
        }

        /// <summary>
        /// Lấy dữ liệu cơ quan từ bộ nhớ đệm (không I/O đĩa).
        /// </summary>
        public static OrganData GetOrgan(string organId)
        {
            if (string.IsNullOrEmpty(organId)) return null;
            if (!isCacheLoaded) PreloadAllData();

            if (DataCache.TryGetValue(organId, out var data)) return data;
            string alt = organId.Replace('_', '-');
            if (DataCache.TryGetValue(alt, out data)) return data;
            string alt2 = organId.Replace('-', '_');
            if (DataCache.TryGetValue(alt2, out data)) return data;

            return null;
        }

        public static DanhMucItem[] GetDanhMuc()
        {
            if (!isCacheLoaded) PreloadAllData();
            return danhMucItems;
        }

        void Awake()
        {
            PreloadAllData();
            EnsureUIHierarchy();
        }

        /// <summary>
        /// Tự động dựng giao diện uGUI kính mờ nếu chưa có.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            if (cardPanel != null && quizPanel != null) return;

            var canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform;

            // 1. Dựng Thẻ kiến thức bên phải màn hình
            if (cardPanel == null)
            {
                var cardGo = new GameObject("OrganKnowledgeCardPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                cardGo.transform.SetParent(root, false);
                cardPanel = (RectTransform)cardGo.transform;
                cardCanvasGroup = cardGo.GetComponent<CanvasGroup>();

                // Tọa độ và kích thước: Rộng 460, neo lề phải, cách viền 30px
                cardPanel.anchorMin = new Vector2(1f, 0f);
                cardPanel.anchorMax = new Vector2(1f, 1f);
                cardPanel.pivot = new Vector2(1f, 0.5f);
                cardPanel.sizeDelta = new Vector2(460f, -120f);
                cardPanel.anchoredPosition = new Vector2(-30f, 0f);

                var bgImg = cardGo.GetComponent<Image>();
                bgImg.sprite = GlassKit.Rounded;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(10f / 255f, 18f / 255f, 34f / 255f, 0.92f);

                // Viền sáng
                var outlineGo = new GameObject("ViềnSáng", typeof(RectTransform), typeof(Image));
                outlineGo.transform.SetParent(cardPanel, false);
                var rtOut = (RectTransform)outlineGo.transform;
                rtOut.anchorMin = Vector2.zero;
                rtOut.anchorMax = Vector2.one;
                rtOut.sizeDelta = Vector2.zero;
                var imgOut = outlineGo.GetComponent<Image>();
                imgOut.sprite = GlassKit.Rounded;
                imgOut.type = Image.Type.Sliced;
                imgOut.color = new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.35f);
                imgOut.raycastTarget = false;

                // Tiêu đề
                titleText = GlassKit.Label(cardPanel, "TieuDe", "🫀 Tim", 22, GlassKit.Ink, true, TextAnchor.MiddleLeft,
                    new Vector2(25f, -22f), new Vector2(360f, 36f), new Vector2(0f, 1f));

                // Chương trình SGK
                programText = GlassKit.Label(cardPanel, "ChuongTrinh", "Sinh học 11 · Tuần hoàn ở động vật", 12, GlassKit.Accent, false, TextAnchor.MiddleLeft,
                    new Vector2(25f, -54f), new Vector2(400f, 24f), new Vector2(0f, 1f));

                // Nút đóng thẻ [✕]
                var btnClose = GlassKit.MakeButton(cardPanel, "NutDongThe", "✕", new Vector2(-20f, -20f), new Vector2(38f, 38f),
                    HideCard, primary: false, fontSize: 18, anchor: new Vector2(1f, 1f));
                closeButton = btnClose;

                // Khung cuộn nội dung
                var scrollGo = new GameObject("ScrollContent", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
                scrollGo.transform.SetParent(cardPanel, false);
                var rtScroll = (RectTransform)scrollGo.transform;
                rtScroll.anchorMin = new Vector2(0f, 0f);
                rtScroll.anchorMax = new Vector2(1f, 1f);
                rtScroll.pivot = new Vector2(0.5f, 0.5f);
                rtScroll.offsetMin = new Vector2(20f, 85f);
                rtScroll.offsetMax = new Vector2(-20f, -85f);

                contentScroll = scrollGo.GetComponent<ScrollRect>();
                contentScroll.horizontal = false;
                contentScroll.vertical = true;
                contentScroll.scrollSensitivity = 25f;

                var textGo = new GameObject("ContentText", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
                textGo.transform.SetParent(scrollGo.transform, false);
                var rtText = (RectTransform)textGo.transform;
                rtText.anchorMin = new Vector2(0f, 1f);
                rtText.anchorMax = new Vector2(1f, 1f);
                rtText.pivot = new Vector2(0f, 1f);
                rtText.sizeDelta = new Vector2(0f, 300f);
                rtText.anchoredPosition = Vector2.zero;

                contentBodyText = textGo.GetComponent<Text>();
                contentBodyText.font = GlassKit.Regular;
                contentBodyText.fontSize = 14;
                contentBodyText.lineSpacing = 1.35f;
                contentBodyText.color = GlassKit.Ink;
                contentBodyText.alignment = TextAnchor.UpperLeft;
                contentBodyText.supportRichText = true;

                var fitter = textGo.GetComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                contentScroll.content = rtText;

                // Khung nút bấm dưới chân thẻ
                quizButton = GlassKit.MakeButton(cardPanel, "NutOnTap", "📝 Ôn tập trắc nghiệm", new Vector2(25f, 25f), new Vector2(195f, 44f),
                    OpenQuizModal, primary: true, fontSize: 14, anchor: new Vector2(0f, 0f));

                microButton = GlassKit.MakeButton(cardPanel, "NutSoiHienVi", "🔬 Soi hiển vi", new Vector2(-25f, 25f), new Vector2(195f, 44f),
                    RequestMicroScene, primary: false, fontSize: 14, anchor: new Vector2(1f, 0f));

                cardCanvasGroup.alpha = 0f;
                cardGo.SetActive(false);
            }

            // 2. Dựng Hộp thoại Ôn tập Trắc nghiệm ở giữa màn hình
            if (quizPanel == null)
            {
                var qGo = new GameObject("OrganQuizModal", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                qGo.transform.SetParent(root, false);
                quizPanel = (RectTransform)qGo.transform;
                quizCanvasGroup = qGo.GetComponent<CanvasGroup>();

                quizPanel.anchorMin = new Vector2(0.5f, 0.5f);
                quizPanel.anchorMax = new Vector2(0.5f, 0.5f);
                quizPanel.pivot = new Vector2(0.5f, 0.5f);
                quizPanel.sizeDelta = new Vector2(620f, 540f);
                quizPanel.anchoredPosition = Vector2.zero;

                var qBg = qGo.GetComponent<Image>();
                qBg.sprite = GlassKit.Rounded;
                qBg.type = Image.Type.Sliced;
                qBg.color = new Color(8f / 255f, 15f / 255f, 30f / 255f, 0.98f);

                // Tiêu đề & Tiến trình
                quizTitleText = GlassKit.Label(quizPanel, "TieuDeTracNghiem", "Ôn tập kiến thức: Tim", 20, GlassKit.Ink, true,
                    TextAnchor.MiddleLeft, new Vector2(30f, -25f), new Vector2(450f, 32f), new Vector2(0f, 1f));

                quizProgressText = GlassKit.Label(quizPanel, "TienDoTracNghiem", "Câu 1 / 4 · Đúng 0", 14, GlassKit.Accent, false,
                    TextAnchor.MiddleRight, new Vector2(-80f, -25f), new Vector2(200f, 32f), new Vector2(1f, 1f));

                closeQuizButton = GlassKit.MakeButton(quizPanel, "NutDongOnTap", "✕", new Vector2(-20f, -20f), new Vector2(38f, 38f),
                    HideQuiz, primary: false, fontSize: 18, anchor: new Vector2(1f, 1f));

                // Nội dung câu hỏi
                var qCardGo = new GameObject("KhungCauHoi", typeof(RectTransform), typeof(Image));
                qCardGo.transform.SetParent(quizPanel, false);
                var rtQCard = (RectTransform)qCardGo.transform;
                rtQCard.anchorMin = new Vector2(0f, 1f);
                rtQCard.anchorMax = new Vector2(1f, 1f);
                rtQCard.pivot = new Vector2(0.5f, 1f);
                rtQCard.sizeDelta = new Vector2(-60f, 85f);
                rtQCard.anchoredPosition = new Vector2(0f, -65f);
                var imgQ = qCardGo.GetComponent<Image>();
                imgQ.sprite = GlassKit.Rounded;
                imgQ.type = Image.Type.Sliced;
                imgQ.color = new Color(20f / 255f, 32f / 255f, 55f / 255f, 0.7f);

                quizQuestionText = GlassKit.Label(qCardGo.transform, "CauHoi", "Câu hỏi ở đây...", 15, GlassKit.Ink, false,
                    TextAnchor.MiddleLeft, new Vector2(15f, 0f), new Vector2(530f, 75f), new Vector2(0f, 0.5f));

                // 4 Phương án lựa chọn A, B, C, D
                for (int i = 0; i < 4; i++)
                {
                    int optIdx = i;
                    float posY = -165f - (i * 54f);

                    var btn = GlassKit.MakeButton(quizPanel, $"NutLuaChon_{i}", $"A. Lựa chọn {i + 1}",
                        new Vector2(30f, posY), new Vector2(560f, 46f), () => OnSelectOption(optIdx),
                        primary: false, fontSize: 14, anchor: new Vector2(0f, 1f));

                    optionButtons[i] = btn;
                    optionTexts[i] = btn.GetComponentInChildren<Text>();
                    optionBackgrounds[i] = btn.GetComponent<Image>();
                }

                // Hộp giải thích kết quả
                var expGo = new GameObject("KhungGiaiThich", typeof(RectTransform), typeof(Image));
                expGo.transform.SetParent(quizPanel, false);
                var rtExp = (RectTransform)expGo.transform;
                rtExp.anchorMin = new Vector2(0f, 0f);
                rtExp.anchorMax = new Vector2(1f, 0f);
                rtExp.pivot = new Vector2(0.5f, 0f);
                rtExp.sizeDelta = new Vector2(-60f, 75f);
                rtExp.anchoredPosition = new Vector2(0f, 70f);
                var imgExp = expGo.GetComponent<Image>();
                imgExp.sprite = GlassKit.Rounded;
                imgExp.type = Image.Type.Sliced;
                imgExp.color = new Color(15f / 255f, 25f / 255f, 42f / 255f, 0.85f);

                quizExplanationText = GlassKit.Label(expGo.transform, "GiaiThich", "Chọn phương án để xem giải thích.", 13,
                    GlassKit.Dim, false, TextAnchor.MiddleLeft, new Vector2(15f, 0f), new Vector2(530f, 65f), new Vector2(0f, 0.5f));

                // Thanh điều hướng: Câu trước / Câu tiếp theo
                prevQuizButton = GlassKit.MakeButton(quizPanel, "NutCauTruoc", "◀ Câu trước", new Vector2(30f, 20f), new Vector2(140f, 40f),
                    PrevQuizQuestion, primary: false, fontSize: 13, anchor: new Vector2(0f, 0f));

                nextQuizButton = GlassKit.MakeButton(quizPanel, "NutCauTiep", "Câu tiếp theo ▶", new Vector2(-30f, 20f), new Vector2(150f, 40f),
                    NextQuizQuestion, primary: true, fontSize: 13, anchor: new Vector2(1f, 0f));

                quizCanvasGroup.alpha = 0f;
                qGo.SetActive(false);
            }
        }

        /// <summary>
        /// Mở thẻ kiến thức cho cơ quan chỉ định.
        /// </summary>
        public void ShowOrgan(string organId)
        {
            EnsureUIHierarchy();

            var data = GetOrgan(organId);
            if (data == null)
            {
                Debug.LogWarning($"[OrganKnowledgeCard] Không tìm thấy dữ liệu cơ quan: '{organId}'");
                return;
            }

            currentOrgan = data;

            // Điền tiêu đề & chương trình
            if (titleText != null) titleText.text = $"{GetIconForOrgan(data.id)}  {data.ten}";
            if (programText != null) programText.text = data.chuong_trinh ?? "";

            // Ghép nội dung phong phú với định dạng số chuẩn Việt Nam
            if (contentBodyText != null)
            {
                contentBodyText.text = FormatOrganKnowledgeText(data);
                if (contentScroll != null) contentScroll.verticalNormalizedPosition = 1f;
            }

            // Kiểm tra xem cơ quan có cảnh hiển vi tương ứng không
            bool hasMicro = HasMicroscopicScene(data.id);
            if (microButton != null)
            {
                microButton.gameObject.SetActive(hasMicro);
            }

            // Hiện card với hiệu ứng Popup
            cardPanel.gameObject.SetActive(true);
            UiMotion.Popup(this, cardPanel, cardCanvasGroup, 0.20f);
        }

        public void HideCard()
        {
            if (cardPanel == null || !cardPanel.gameObject.activeSelf) return;

            HideQuiz();
            UiMotion.Dismiss(this, cardPanel, cardCanvasGroup, 0.14f, () =>
            {
                if (cardPanel != null) cardPanel.gameObject.SetActive(false);
                OnClosed?.Invoke();
            });
        }

        void RequestMicroScene()
        {
            if (currentOrgan != null)
            {
                OnRequestMicroScene?.Invoke(currentOrgan.id);
            }
        }

        // =====================================================================
        // Xử lý Hộp thoại Ôn tập Trắc nghiệm
        // =====================================================================

        public void OpenQuizModal()
        {
            if (currentOrgan == null || currentOrgan.trac_nghiem == null || currentOrgan.trac_nghiem.Length == 0)
            {
                return;
            }

            currentQuizIndex = 0;
            correctAnswersCount = 0;

            quizPanel.gameObject.SetActive(true);
            UiMotion.Popup(this, quizPanel, quizCanvasGroup, 0.20f);

            RenderCurrentQuizQuestion();
        }

        public void HideQuiz()
        {
            if (quizPanel == null || !quizPanel.gameObject.activeSelf) return;

            UiMotion.Dismiss(this, quizPanel, quizCanvasGroup, 0.14f, () =>
            {
                if (quizPanel != null) quizPanel.gameObject.SetActive(false);
            });
        }

        void RenderCurrentQuizQuestion()
        {
            if (currentOrgan == null || currentOrgan.trac_nghiem == null) return;
            var questions = currentOrgan.trac_nghiem;
            if (currentQuizIndex < 0 || currentQuizIndex >= questions.Length) return;

            var q = questions[currentQuizIndex];
            hasAnsweredCurrentQuestion = false;

            if (quizTitleText != null) quizTitleText.text = $"Ôn tập: {currentOrgan.ten}";
            if (quizProgressText != null)
            {
                quizProgressText.text = $"Câu {currentQuizIndex + 1}/{questions.Length} · Đúng {correctAnswersCount}";
            }

            if (quizQuestionText != null) quizQuestionText.text = FormatDecimal(q.cau);

            string[] optPrefix = new string[] { "A. ", "B. ", "C. ", "D. " };
            for (int i = 0; i < 4; i++)
            {
                if (optionButtons[i] == null) continue;

                if (q.lua_chon != null && i < q.lua_chon.Length)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].interactable = true;
                    if (optionTexts[i] != null) optionTexts[i].text = optPrefix[i] + FormatDecimal(q.lua_chon[i]);
                    if (optionBackgrounds[i] != null)
                    {
                        // Màu kính mặc định
                        optionBackgrounds[i].color = new Color(28f / 255f, 44f / 255f, 74f / 255f, 0.75f);
                    }
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            if (quizExplanationText != null)
            {
                quizExplanationText.text = "Chọn một phương án để kiểm tra kết quả và xem giải thích chi tiết.";
                quizExplanationText.color = GlassKit.Dim;
            }

            if (prevQuizButton != null) prevQuizButton.interactable = currentQuizIndex > 0;
            if (nextQuizButton != null)
            {
                var txt = nextQuizButton.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = (currentQuizIndex == questions.Length - 1) ? "Hoàn thành ✓" : "Câu tiếp theo ▶";
                }
            }
        }

        void OnSelectOption(int index)
        {
            if (hasAnsweredCurrentQuestion) return;
            if (currentOrgan == null || currentOrgan.trac_nghiem == null) return;
            var q = currentOrgan.trac_nghiem[currentQuizIndex];

            hasAnsweredCurrentQuestion = true;
            char correctChar = !string.IsNullOrEmpty(q.dap_an) ? char.ToUpper(q.dap_an[0]) : 'A';
            int correctIndex = correctChar - 'A';

            bool isCorrect = (index == correctIndex);
            if (isCorrect) correctAnswersCount++;

            // Cập nhật màu các phương án
            for (int i = 0; i < 4; i++)
            {
                if (optionBackgrounds[i] == null) continue;

                if (i == correctIndex)
                {
                    // Màu xanh lá chuẩn
                    optionBackgrounds[i].color = new Color(16f / 255f, 185f / 255f, 129f / 255f, 0.90f);
                }
                else if (i == index && !isCorrect)
                {
                    // Màu đỏ nổi bật
                    optionBackgrounds[i].color = new Color(239f / 255f, 68f / 255f, 68f / 255f, 0.90f);
                }
                else
                {
                    // Làm mờ phương án khác
                    optionBackgrounds[i].color = new Color(18f / 255f, 26f / 255f, 44f / 255f, 0.40f);
                }
            }

            if (quizProgressText != null)
            {
                quizProgressText.text = $"Câu {currentQuizIndex + 1}/{currentOrgan.trac_nghiem.Length} · Đúng {correctAnswersCount}";
            }

            if (quizExplanationText != null)
            {
                string header = isCorrect ? "<color=#34D399><b>✓ Chính xác!</b></color> " : "<color=#F87171><b>✗ Chưa đúng!</b></color> ";
                quizExplanationText.text = header + FormatDecimal(q.giai_thich ?? "");
                quizExplanationText.color = GlassKit.Ink;
            }
        }

        void NextQuizQuestion()
        {
            if (currentOrgan == null || currentOrgan.trac_nghiem == null) return;
            if (currentQuizIndex < currentOrgan.trac_nghiem.Length - 1)
            {
                currentQuizIndex++;
                RenderCurrentQuizQuestion();
            }
            else
            {
                HideQuiz();
            }
        }

        void PrevQuizQuestion()
        {
            if (currentQuizIndex > 0)
            {
                currentQuizIndex--;
                RenderCurrentQuizQuestion();
            }
        }

        // =====================================================================
        // Bộ định dạng chuỗi kiến thức & số học chuẩn Việt Nam
        // =====================================================================

        string FormatOrganKnowledgeText(OrganData data)
        {
            var sb = new System.Text.StringBuilder();

            AppendSection(sb, "📍 VỊ TRÍ", data.vi_tri);
            AppendSection(sb, "🔬 CẤU TẠO", data.cau_tao);
            AppendSection(sb, "⚡ CHỨC NĂNG", data.chuc_nang);
            AppendSection(sb, "🔄 CƠ CHẾ SINH LÝ", data.co_che);
            AppendSection(sb, "💡 LIÊN HỆ THỰC TẾ & BỆNH HỌC", data.lien_he);
            AppendSection(sb, "📝 ĐIỂM TRỌNG TÂM HAY THI", data.diem_thi);

            return sb.ToString();
        }

        void AppendSection(System.Text.StringBuilder sb, string heading, string[] items)
        {
            if (items == null || items.Length == 0) return;

            sb.Append("<color=#22D3EE><b>").Append(heading).Append("</b></color>\n");
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item)) continue;
                sb.Append("  • ").Append(FormatDecimal(item)).Append("\n");
            }
            sb.Append("\n");
        }

        public static string FormatDecimal(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Chuyển dấu chấm số thập phân trong ngữ cảnh số sang dấu phẩy: ví dụ "0.8 s" -> "0,8 s", "125.5 mL" -> "125,5 mL"
            return System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)\.(\d+)", "$1,$2");
        }

        public static string GetIconForOrgan(string organId)
        {
            if (string.IsNullOrEmpty(organId)) return "🔬";
            switch (organId.ToLower())
            {
                case "tim": return "🫀";
                case "mach_mau": case "mach-mau": return "🩸";
                case "phoi": return "🫁";
                case "nao": return "🧠";
                case "tuy_song": case "tuy-song": return "⚡";
                case "than": return "🫘";
                case "da_day": case "da-day": return "🟠";
                case "ruot_non": case "ruot-non": return "🌀";
                case "ruot_gia": case "ruot-gia": return "🟫";
                case "gan": return "🟤";
                case "tuy": return "🟡";
                case "lach": return "🛡️";
                case "xuong": return "🦴";
                case "co": return "💪";
                case "da": return "🖐️";
                default: return "🔬";
            }
        }

        public static bool HasMicroscopicScene(string organId)
        {
            if (string.IsNullOrEmpty(organId)) return false;
            switch (organId.ToLower())
            {
                case "phoi":
                case "than":
                case "nao":
                case "co":
                case "ruot_non":
                case "ruot-non":
                case "da_day":
                case "da-day":
                case "gan":
                case "tim": // Tim tương ứng với sarcomere cơ tim
                    return true;
                default:
                    return false;
            }
        }
    }
}
