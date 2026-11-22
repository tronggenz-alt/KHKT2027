using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Demo.Ai;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Trang "Trợ lý AI" ở sảnh: mỗi giáo viên / học sinh tự nhập khóa API AI đám mây (Gemini, Groq, Claude, OpenAI).
    /// Có khóa thì robot và chú thích hiện tượng trong lab gọi thẳng nhà cung cấp; không có thì dùng AI của máy chủ như cũ.
    /// Ô khóa không bao giờ hiện lại khóa đã lưu, chỉ hiện dạng che.
    /// </summary>
    public class PageAiKey : MonoBehaviour
    {
        const float X = 56f, FieldW = 620f, FieldH = 48f;

        RectTransform root;
        readonly Dictionary<string, Button> providerButtons = new Dictionary<string, Button>();
        InputField modelField, keyField;
        Text modelHint, keyHint, status, current, note;
        Button checkButton, saveButton, deleteButton;
        string provider = ApiKeyStore.Gemini;
        bool busy;

        public string Provider => provider;
        public InputField ModelField => modelField;
        public InputField KeyField => keyField;
        public string StatusText => status != null ? status.text : "";
        public string CurrentText => current != null ? current.text : "";
        public bool Busy => busy;

        public void Build(RectTransform page)
        {
            root = page;
            GlassKit.Label(page, "Tiêu đề", "Trợ lý AI", 36, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(X, -48), new Vector2(900, 50));
            GlassKit.Label(page, "Mô tả", "Dùng khóa API AI của riêng bạn: robot và trợ lý trong lab gọi thẳng nhà cung cấp, không qua máy chủ của trường.",
                           18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(X, -100), new Vector2(980, 30));

            var card = MenuKit.Card(page, "Thẻ", 0f, 1f, -150, 600, 0f, false);
            card.offsetMin = new Vector2(X, card.offsetMin.y);
            card.offsetMax = new Vector2(-X, card.offsetMax.y);
            var body = MenuKit.Body(card);
            float x = 32, y = -28;

            current = GlassKit.Label(body, "Đang dùng", "", 17, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(980, 26));
            y -= 46;

            GlassKit.Label(body, "Nhà cung cấp (nhãn)", "Nhà cung cấp", 16, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(400, 24));
            y -= 30;
            float bx = x;
            foreach (var p in ApiKeyStore.Providers)
            {
                string pp = p;
                var b = MenuKit.MakeButton(body, "Chọn " + ApiKeyStore.ProviderName(p), ApiKeyStore.ProviderName(p), new Vector2(bx, y), new Vector2(190, 44), () => SelectProvider(pp), 1);
                providerButtons[p] = b;
                bx += 200;
            }
            y -= 66;

            modelField = MakeField(body, "Ô mô hình", "Mô hình", x, ref y, false, out modelHint);
            keyField = MakeField(body, "Ô khóa", "Khóa API", x, ref y, true, out keyHint);

            checkButton = MenuKit.MakeButton(body, "Nút kiểm tra khóa", "Kiểm tra khóa", new Vector2(x, y), new Vector2(180, 46), Check, 1);
            saveButton = MenuKit.MakeButton(body, "Nút lưu khóa", "Lưu khóa", new Vector2(x + 192, y), new Vector2(160, 46), Save, 0);
            deleteButton = MenuKit.MakeButton(body, "Nút xóa khóa", "Xóa khóa", new Vector2(x + 364, y), new Vector2(140, 46), Delete, 2);
            y -= 62;

            status = GlassKit.Label(body, "Trạng thái", "", 16, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(980, 48));
            y -= 56;
            note = GlassKit.Label(body, "Ghi chú", "", 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(980, 70));
        }

        InputField MakeField(Transform parent, string name, string label, float x, ref float y, bool password, out Text hint)
        {
            GlassKit.Label(parent, name + " (nhãn)", label, 16, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(x, y), new Vector2(FieldW, 24));
            var size = new Vector2(FieldW, FieldH);
            var outer = MenuKit.Fill(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 26), size, MenuKit.Line, MenuKit.RadiusButton);
            var inner = MenuKit.Fill(outer.transform, "Nền", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(3, 3), MenuKit.Paper, MenuKit.RadiusButton);
            inner.raycastTarget = true;
            var text = GlassKit.Label(outer.transform, "Chữ", "", 18, MenuKit.Ink, false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(FieldW - 28, FieldH));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint = GlassKit.Label(outer.transform, "Gợi ý", "", 17, MenuKit.WithAlpha(MenuKit.InkDim, 0.7f), false, TextAnchor.MiddleLeft, new Vector2(14, 0), new Vector2(FieldW - 28, FieldH));
            hint.supportRichText = false;
            var f = outer.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = hint;
            f.targetGraphic = inner;
            f.caretColor = MenuKit.Accent;
            f.customCaretColor = true;
            f.selectionColor = new Color(MenuKit.Accent.r, MenuKit.Accent.g, MenuKit.Accent.b, 0.25f);
            var colors = f.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = MenuKit.PaperAlt;
            colors.selectedColor = MenuKit.AccentSoft;
            f.colors = colors;
            f.lineType = InputField.LineType.SingleLine;
            f.characterLimit = 300;
            if (password)
            {
                f.contentType = InputField.ContentType.Password;
                f.asteriskChar = '•';
            }
            y -= 26 + FieldH + 22;
            return f;
        }

        /// <summary>Mỗi lần mở trang: đọc khóa đã lưu của tài khoản đang đăng nhập (không hiện khóa thật).</summary>
        public void Open()
        {
            var saved = ApiKeyStore.Current;
            provider = saved != null ? saved.provider : ApiKeyStore.Gemini;
            modelField.text = saved != null ? saved.model ?? "" : "";
            keyField.text = "";
            SetStatus("", MenuKit.InkDim);
            Refresh();
        }

        public void SelectProvider(string p)
        {
            if (busy) return;
            provider = p;
            var saved = ApiKeyStore.Current;
            modelField.text = saved != null && saved.provider == p ? saved.model ?? "" : "";
            SetStatus("", MenuKit.InkDim);
            Refresh();
        }

        void Refresh()
        {
            var saved = ApiKeyStore.Current;
            var u = GameSession.User;
            foreach (var kv in providerButtons)
            {
                bool on = kv.Key == provider;
                var img = kv.Value.GetComponent<Image>();
                img.color = on ? MenuKit.Accent : MenuKit.Line;
                var label = kv.Value.transform.Find("Chữ")?.GetComponent<Text>();
                if (label != null) { label.color = on ? MenuKit.Accent : MenuKit.Ink; label.font = on ? GlassKit.Semibold : GlassKit.Regular; }
            }
            string def = ApiKeyStore.DefaultModel(provider);
            modelHint.text = def.Length > 0 ? $"Để trống: {def}" : "Để trống: tự chọn mô hình mạnh nhất khóa dùng được";
            keyHint.text = saved != null && saved.provider == provider
                ? $"Đã lưu {ApiKeyStore.Mask(saved.key)} · nhập khóa mới để thay"
                : $"Dán khóa {ApiKeyStore.ProviderName(provider)} (thường bắt đầu bằng {ApiKeyStore.KeyPrefix(provider)})";
            current.text = u == null ? "Khách không dùng được khóa riêng: hãy đăng nhập."
                : saved != null ? $"Đang dùng: khóa riêng {ApiKeyStore.ProviderName(saved.provider)} {ApiKeyStore.Mask(saved.key)} · mô hình {(saved.ModelOrDefault.Length > 0 ? saved.ModelOrDefault : "tự chọn")}"
                : "Đang dùng: AI của máy chủ trường (chưa có khóa riêng)";
            current.color = saved != null ? MenuKit.Bio : MenuKit.Ink;
            deleteButton.interactable = saved != null && !busy;
            checkButton.interactable = saveButton.interactable = u != null && !busy;
            note.text = (ApiKeyStore.UsesDpapi
                    ? "Khóa được mã hóa bằng DPAPI của Windows, chỉ lưu trên máy này và chỉ tài khoản Windows này giải được. "
                    : "Khóa được mã hóa AES theo mã máy và chỉ lưu trên máy này (trên máy không phải Windows, cách này chỉ che khóa, không chắc bằng DPAPI). ") +
                "Khóa riêng theo từng tài khoản, không gửi lên máy chủ, không ghi vào nhật ký. Chi phí gọi AI tính vào tài khoản của khóa. " +
                "Khi giáo viên tắt AI, bật kiểm tra hay khóa hóa chất, robot vẫn tuân theo.";
        }

        void SetStatus(string text, Color c)
        {
            status.text = text;
            status.color = c;
        }

        /// <summary>Khóa đang nhập, hoặc khóa đã lưu nếu ô trống và cùng nhà cung cấp.</summary>
        ApiKeyStore.Entry Draft()
        {
            string key = (keyField.text ?? "").Trim();
            var saved = ApiKeyStore.Current;
            if (key.Length == 0 && saved != null && saved.provider == provider) key = saved.key;
            return new ApiKeyStore.Entry { provider = provider, model = (modelField.text ?? "").Trim(), key = key };
        }

        public void Check()
        {
            if (busy) return;
            var e = Draft();
            if (!e.Valid) { SetStatus("Hãy dán khóa API trước.", MenuKit.Error); return; }
            busy = true;
            SetStatus("Đang kiểm tra khóa với " + ApiKeyStore.ProviderName(provider) + "...", MenuKit.InkDim);
            Refresh();
            StartCoroutine(DirectAi.Check(e, m =>
            {
                busy = false;
                SetStatus($"Khóa dùng được. Mô hình: {m}.", MenuKit.Bio);
                Refresh();
            }, err =>
            {
                busy = false;
                SetStatus(err, MenuKit.Error);
                Refresh();
            }));
        }

        public void Save()
        {
            if (busy) return;
            string owner = ApiKeyStore.Owner;
            if (owner == null) { SetStatus("Cần đăng nhập để lưu khóa.", MenuKit.Error); return; }
            var e = Draft();
            if (!e.Valid) { SetStatus("Hãy dán khóa API trước.", MenuKit.Error); return; }
            try { ApiKeyStore.Save(owner, e); }
            catch (System.Exception ex)
            {
                SetStatus("Không lưu được khóa (" + ex.GetType().Name + ").", MenuKit.Error);
                return;
            }
            keyField.text = "";
            string prefix = ApiKeyStore.KeyPrefix(provider);
            bool odd = !ApiKeyStore.LooksLike(provider, e.key);
            SetStatus("Đã lưu khóa đã mã hóa trên máy này." + (odd ? $" Lưu ý: khóa {ApiKeyStore.ProviderName(provider)} thường bắt đầu bằng {prefix}; bấm Kiểm tra khóa để chắc." : " Bấm Kiểm tra khóa để thử."),
                      odd ? MenuKit.Warn : MenuKit.Bio);
            Refresh();
        }

        public void Delete()
        {
            if (busy) return;
            ApiKeyStore.Delete(ApiKeyStore.Owner);
            keyField.text = "";
            modelField.text = "";
            SetStatus("Đã xóa khóa riêng. Robot quay về dùng AI của máy chủ trường.", MenuKit.InkDim);
            Refresh();
        }
    }
}
