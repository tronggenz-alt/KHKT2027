using System.Collections.Generic;
using Khkt.Interaction;
using Khkt.Simulation.Chem;
using UnityEngine;
using UnityEngine.UI;
using static Khkt.Demo.GlassKit;

namespace Khkt.Demo
{
    /// <summary>
    /// Chú thích hiện tượng tự phát: mỗi khi bộ máy hóa học phát hiện kết tủa, khí, đổi màu, tỏa nhiệt hay
    /// nguy hiểm, một card kính mờ hiện tên hiện tượng, phương trình và giải thích ngắn (soạn sẵn, đúng theo
    /// mô phỏng). Nếu máy chủ AI chạy, card có thêm một dòng "✦ AI" diễn giải theo đúng ngữ cảnh của bình đó.
    /// Sự cố nguy hiểm được chuyển cho robot trợ lý xử lý.
    /// </summary>
    public class ReactionAnnotator : MonoBehaviour
    {
        public ModernUI ui;
        public int maxCards = 3;

        public static ReactionAnnotator Instance { get; private set; }
        /// <summary>Hiện tượng gần nhất (robot dùng để trả lời "hiện tượng vừa rồi là gì").</summary>
        public ChemEvent Last { get; private set; }
        public LabContainer LastContainer { get; private set; }
        public string LastAi { get; private set; }
        public readonly List<string> History = new List<string>();

        class Card
        {
            public RectTransform rt;
            public CanvasGroup group;
            public Text title, eq, detail, ai;
            public Image dot;
            public float born, life;
            public ChemEvent e;
        }

        readonly List<Card> cards = new List<Card>();
        Material glass;

        void Awake() => Instance = this;

        void Start()
        {
            glass = Glass(ui.glassShader != null ? ui.glassShader : Shader.Find("Khkt/KinhMo"));
            LabContainer.ChemEventRaised += OnEvent;
        }

        void OnDestroy() => LabContainer.ChemEventRaised -= OnEvent;

        void OnEvent(LabContainer c, ChemEvent e)
        {
            if (c == null || !c.reactive) return;
            Last = e;
            LastContainer = c;
            LastAi = null;
            History.Add($"{e.Title}{(e.Equation != null ? " (" + e.Equation + ")" : "")} trong {c.label}");
            if (History.Count > 12) History.RemoveAt(0);
            var card = Show(e, c);
            AiClient.Instance?.Log(e.Severity >= Severity.Warning ? "su_co" : "hien_tuong", e.Title, $"{e.Equation} · {c.label}: {c.Total.Describe(5)}", e.Severity.ToString());
            if (e.Severity >= Severity.Warning && LabRobot.Instance != null) LabRobot.Instance.HandleIncident(c, e);
            RequestAi(card, c, e);
        }

        Card Show(ChemEvent e, LabContainer c)
        {
            // cùng loại hiện tượng đang hiện thì làm mới card đó
            foreach (var old in cards)
                if (old.e.Key == e.Key) { old.born = Time.time; return old; }
            if (cards.Count >= maxCards) { Destroy(cards[0].rt.gameObject); cards.RemoveAt(0); }
            var img = Panel(ui.Canvas.transform, "Chú thích hiện tượng", new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -90), new Vector2(400, 120), glass, 1.3f);
            var card = new Card { rt = img.rectTransform, group = img.gameObject.AddComponent<CanvasGroup>(), born = Time.time, e = e,
                                  life = e.Severity >= Severity.Warning ? 22f : 14f };
            var dotRt = Rect(card.rt, "Chấm", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -22), new Vector2(10, 10));
            card.dot = dotRt.gameObject.AddComponent<Image>();
            card.dot.sprite = SolidCircle;
            card.dot.color = e.Severity == Severity.Danger ? Bad : e.Severity == Severity.Warning ? Warn : e.Kind == EventKind.Gas ? Accent : Good;
            string where = c != null ? c.label : "";
            card.title = Label(card.rt, "Tên hiện tượng", e.Title, 17, Ink, true, TextAnchor.UpperLeft, new Vector2(36, -12), new Vector2(350, 26));
            card.eq = Label(card.rt, "Phương trình", e.Equation ?? "", 15, Accent, false, TextAnchor.UpperLeft, new Vector2(36, -40), new Vector2(350, 22));
            card.detail = Label(card.rt, "Giải thích", (e.Detail ?? "") + (where.Length > 0 ? $"  <color=#9FB4C8>· {where}</color>" : ""), 14, Dim, false, TextAnchor.UpperLeft, new Vector2(36, -64), new Vector2(350, 40));
            card.ai = Label(card.rt, "AI", "", 14, new Color(0.75f, 0.92f, 0.85f, 0.95f), false, TextAnchor.UpperLeft, new Vector2(36, -100), new Vector2(350, 40));
            cards.Add(card);
            return card;
        }

        void RequestAi(Card card, LabContainer c, ChemEvent e)
        {
            var ai = AiClient.Instance;
            if (ai == null || !ai.Online) return;
            var req = new AiClient.AnnotateRequest
            {
                tieu_de = e.Title, phuong_trinh = e.Equation ?? "", chi_tiet = e.Detail ?? "", muc_do = e.Severity.ToString(), loai = e.Kind.ToString(),
                thanh_phan = ChemColor.Components(c.Total.Mix, c.VolumeMl, 6).ToArray(),
                ph = (float)c.BulkPh, nhiet_do = c.TemperatureC,
                ngu_canh = $"{c.label}: {c.Total.Describe(6)}",
            };
            card.ai.text = "✦ AI đang giải thích...";
            ai.Annotate(req, r =>
            {
                if (card.rt == null) return;
                LastAi = r.giai_thich;
                card.ai.text = string.IsNullOrEmpty(r.giai_thich) ? "" : "✦ " + r.giai_thich;
                card.born = Time.time;
            }, _ => { if (card.rt != null) card.ai.text = ""; });
        }

        void LateUpdate()
        {
            float y = -90;
            if (ui != null && ui.StepperVisible) y -= 70;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var c = cards[i];
                float age = Time.time - c.born;
                if (age > c.life || c.rt == null)
                {
                    if (c.rt != null) Destroy(c.rt.gameObject);
                    cards.RemoveAt(i);
                }
            }
            foreach (var c in cards)
            {
                float age = Time.time - c.born;
                c.group.alpha = Mathf.Clamp01(Mathf.Min(age * 5f, (c.life - age) / 1.5f));
                // xếp dòng theo chiều cao thật
                float yy = -12 - Mathf.Max(24, c.title.preferredHeight) - 4;
                c.eq.gameObject.SetActive(c.eq.text.Length > 0);
                if (c.eq.text.Length > 0) { c.eq.rectTransform.anchoredPosition = new Vector2(36, yy); yy -= c.eq.preferredHeight + 4; }
                c.detail.rectTransform.anchoredPosition = new Vector2(36, yy);
                yy -= c.detail.preferredHeight + 4;
                c.ai.gameObject.SetActive(c.ai.text.Length > 0);
                if (c.ai.text.Length > 0) { c.ai.rectTransform.anchoredPosition = new Vector2(36, yy); yy -= c.ai.preferredHeight + 4; }
                float h = -yy + 10;
                c.rt.sizeDelta = new Vector2(400, h);
                c.rt.anchoredPosition = Vector2.Lerp(c.rt.anchoredPosition, new Vector2(20, y), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                y -= h + 10;
            }
        }
    }
}
