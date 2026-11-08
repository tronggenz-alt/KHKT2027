using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Menu.Dashboard
{
    /// <summary>
    /// Trang Tổng quan: điểm trung bình, số bài đã làm, tiến bộ, lối vào phòng thí nghiệm, kết quả và nhận xét gần đây.
    /// Mọi con số lấy từ máy chủ; chưa có dữ liệu thì nói rõ là chưa có, không hiện số giả.
    /// </summary>
    public class PageOverview : MonoBehaviour
    {
        const float Pad = 56f, Gap = 20f, RowH = 62f;
        const int MaxRows = 5;

        DashboardShell shell;
        RectTransform rt;
        RectTransform title, subtitle, cardLab, cardRecent, cardComment, metricsRow;
        readonly List<RectTransform> metricCards = new List<RectTransform>();

        Text titleText, subtitleText, notice;
        Text avgValue, avgNote, doneValue, doneNote, growthValue, growthNote;
        RectTransform recentList;
        Text recentEmpty, commentTitle, commentBody;
        Button retry;
        RectTransform noticeBar;
        Image noticeBg;
        RectTransform skeleton;
        bool loading, skeletonShown, hadData;

        public Text Status => notice;
        public bool Loading => loading;
        public int RowsShown => recentList.childCount;

        public void Build(DashboardShell owner)
        {
            shell = owner;
            rt = (RectTransform)transform;

            title = GlassKit.Label(rt, "Lời chào", "", 40, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(Pad, -44), new Vector2(900, 54)).rectTransform;
            titleText = title.GetComponent<Text>();
            subtitle = GlassKit.Label(rt, "Dòng lớp", "", 18, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(Pad, -100), new Vector2(900, 28)).rectTransform;
            subtitleText = subtitle.GetComponent<Text>();

            BuildNotice();

            // Hàng số liệu nằm trong khung có lề hai bên để các thẻ chia đều theo phần của chiều rộng
            metricsRow = Row("Hàng số liệu", -144, 150);
            avgValue = Metric(0, "Thẻ điểm trung bình", "Điểm trung bình", MenuKit.IconScore, MenuKit.AccentSoft, MenuKit.Accent, out avgNote);
            doneValue = Metric(1, "Thẻ bài đã làm", "Bài đã làm", MenuKit.IconDone, MenuKit.BioSoft, MenuKit.Bio, out doneNote);
            growthValue = Metric(2, "Thẻ tiến bộ", "Tiến bộ", MenuKit.IconGrowth, MenuKit.WarnSoft, MenuKit.Warn, out growthNote);

            BuildLabCard();
            BuildBottomCards();
        }

        RectTransform Row(string name, float y, float h)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(rt, false);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(Pad - Gap * 0.5f, y - h);
            row.offsetMax = new Vector2(-Pad + Gap * 0.5f, y);
            return row;
        }

        Text Metric(int index, string name, string label, string glyph, Color soft, Color ink, out Text note)
        {
            var card = MenuKit.Card(metricsRow, name, index / 3f, (index + 1) / 3f, 0, 150, Gap);
            metricCards.Add(card);
            var body = MenuKit.Body(card);
            GlassKit.Label(body, "Nhãn", label, 17, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(24, -20), new Vector2(260, 26));
            var badge = MenuKit.IconBadge(body, "Ô biểu tượng", glyph, Vector2.zero, 44, soft, ink).rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(0, 1);
            badge.anchoredPosition = new Vector2(-68, -20);
            var value = GlassKit.Label(body, "Số: " + label, "–", 44, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(24, -48), new Vector2(260, 58));
            note = GlassKit.Label(body, "Ghi chú: " + label, "", 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(24, -112), new Vector2(340, 24));
            return value;
        }

        void BuildNotice()
        {
            noticeBar = GlassKit.Rect(rt, "Thanh thông báo", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Pad, -48), new Vector2(560, 52));
            noticeBg = MenuKit.Paint(noticeBar, MenuKit.WarnSoft, MenuKit.RadiusButton);
            notice = GlassKit.Label(noticeBar, "Thông báo trang chính", "", 16, MenuKit.Warn, false, TextAnchor.MiddleLeft, new Vector2(18, 0), new Vector2(420, 52));
            retry = MenuKit.MakeButton(noticeBar, "Nút Thử lại", "Thử lại", new Vector2(-10, 0), new Vector2(96, 36), Refresh, 3, MenuKit.RadiusButton, new Vector2(1, 0.5f));
            retry.transform.Find("Chữ").GetComponent<Text>().fontSize = 15;
            noticeBar.gameObject.SetActive(false);
        }

        void BuildLabCard()
        {
            var row = Row("Hàng phòng thí nghiệm", -314, 136);
            cardLab = MenuKit.Card(row, "Thẻ phòng thí nghiệm", 0f, 1f, 0, 136, Gap);
            var body = MenuKit.Body(cardLab);
            body.GetComponent<Image>().color = MenuKit.Accent;
            GlassKit.Label(body, "Tiêu đề thẻ", "Phòng thí nghiệm", 26, Color.white, true, TextAnchor.UpperLeft, new Vector2(32, -30), new Vector2(620, 36));
            GlassKit.Label(body, "Mô tả thẻ", "Tự tay làm thí nghiệm Hóa học, Sinh học trong phòng lab ảo.", 17, MenuKit.WithAlpha(Color.white, 0.86f), false, TextAnchor.UpperLeft, new Vector2(32, -70), new Vector2(700, 28));
            var size = new Vector2(240, 56);
            MenuKit.MakeButton(body, "Nút Vào phòng thí nghiệm", "Vào phòng thí nghiệm", new Vector2(-32, 0), size, () => shell.EnterLab?.Invoke(), 3, MenuKit.RadiusButton, new Vector2(1, 0.5f));
            MenuKit.MakeButton(body, "Nút chuyển Phòng học chung", "Phòng học chung", new Vector2(-284, 0), new Vector2(190, 56), () => shell.OpenRoom?.Invoke(), 0, MenuKit.RadiusButton, new Vector2(1, 0.5f))
                   .GetComponent<Image>().color = MenuKit.AccentDeep;
        }

        void BuildBottomCards()
        {
            var row = new GameObject("Hàng dưới", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(rt, false);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = new Vector2(Pad - Gap * 0.5f, 40);
            row.offsetMax = new Vector2(-Pad + Gap * 0.5f, -470);

            cardRecent = MenuKit.Card(row, "Thẻ kết quả gần đây", 0f, 0.62f, 0, -0.001f, Gap);
            var body = MenuKit.Body(cardRecent);
            GlassKit.Label(body, "Tiêu đề thẻ", "Kết quả gần đây", 20, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(24, -20), new Vector2(500, 30));
            recentList = new GameObject("Danh sách kết quả", typeof(RectTransform)).GetComponent<RectTransform>();
            recentList.SetParent(body, false);
            recentList.anchorMin = new Vector2(0, 1);
            recentList.anchorMax = new Vector2(1, 1);
            recentList.pivot = new Vector2(0.5f, 1);
            recentList.offsetMin = new Vector2(24, -60 - RowH * MaxRows);
            recentList.offsetMax = new Vector2(-24, -60);
            recentEmpty = GlassKit.Label(body, "Chưa có kết quả", "", 17, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(24, -66), new Vector2(600, 52));

            cardComment = MenuKit.Card(row, "Thẻ nhận xét", 0.62f, 1f, 0, -0.001f, Gap);
            var cb = MenuKit.Body(cardComment);
            GlassKit.Label(cb, "Tiêu đề thẻ", "Nhận xét mới nhất", 20, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(24, -20), new Vector2(420, 30));
            commentTitle = GlassKit.Label(cb, "Bài được nhận xét", "", 15, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(24, -60), new Vector2(420, 24));
            commentBody = GlassKit.Label(cb, "Nội dung nhận xét", "", 18, MenuKit.Ink, false, TextAnchor.UpperLeft, new Vector2(24, -92), new Vector2(420, 220));
            commentBody.lineSpacing = 1.2f;
            commentBody.verticalOverflow = VerticalWrapMode.Truncate;
            var cr = commentBody.rectTransform;
            cr.anchorMax = new Vector2(1, 1);
            cr.sizeDelta = new Vector2(-48, 220);
            cr.anchoredPosition = new Vector2(24, -92);
        }

        // =====================================================================
        // Mở trang, tải dữ liệu
        // =====================================================================

        /// <summary>Gọi mỗi lần chuyển vào trang này: chạy chuyển động vào rồi cập nhật dữ liệu.</summary>
        public void Open()
        {
            var u = GameSession.User;
            titleText.text = u != null ? $"Chào {MenuKit.GivenName(u.ho_ten)}" : "Chế độ dùng thử";
            subtitleText.text = u != null ? MenuKit.ClassLine(u) : "Kết quả sẽ không được lưu vào tài khoản.";

            bool student = u != null && u.vai_tro == "hoc_sinh";
            metricsRow.gameObject.SetActive(student);
            cardRecent.parent.gameObject.SetActive(student);
            // không có hàng số liệu thì thẻ phòng thí nghiệm dồn lên, tránh khoảng trống giữa trang
            var labRow = (RectTransform)cardLab.parent;
            labRow.offsetMax = new Vector2(labRow.offsetMax.x, student ? -314 : -144);
            labRow.offsetMin = new Vector2(labRow.offsetMin.x, (student ? -314 : -144) - 136);

            MenuAnim.Enter(title, 0f, 12f, 0.3f);
            MenuAnim.Enter(subtitle, 0.04f, 12f, 0.3f);
            for (int i = 0; i < metricCards.Count; i++) MenuAnim.Enter(metricCards[i], 0.06f + i * 0.05f);
            MenuAnim.Enter(cardLab, 0.2f);
            MenuAnim.Enter(cardRecent, 0.26f);
            MenuAnim.Enter(cardComment, 0.31f);

            if (student)
            {
                if (StudentData.HasData) Render(true);
                else ShowEmptyValues();
                Refresh();
            }
            else HideNotice();
        }

        /// <summary>Tải lại ở nền. Đang có số cũ thì giữ nguyên trên màn hình cho tới khi có số mới.</summary>
        public void Refresh()
        {
            if (loading || GameSession.User == null || GameSession.User.vai_tro != "hoc_sinh") return;
            loading = true;
            hadData = StudentData.HasData;
            HideNotice();
            if (!hadData) ShowSkeleton();
            shell.Host.StartCoroutine(StudentData.Load(OnLoaded));
        }

        void OnLoaded(DataState state, bool final)
        {
            if (final) loading = false;
            HideSkeleton();
            switch (state)
            {
                case DataState.Ready:
                    HideNotice();
                    // đang có số cũ trên màn hình thì đổi thẳng, không cho số chạy lại từ 0
                    Render(hadData);
                    hadData = true;
                    break;
                case DataState.Stale:
                    // câu trả lời của người trước; người mới đăng nhập cần tải lại phần của mình
                    if (isActiveAndEnabled) Refresh();
                    break;
                case DataState.Offline:
                    ShowNotice(StudentData.HasData ? "Mất kết nối: đang hiện số liệu lần tải trước." : "Không tải được kết quả. Bạn vẫn vào lab được.");
                    break;
                case DataState.Expired:
                    shell.SessionExpired?.Invoke("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.");
                    break;
            }
        }

        void ShowEmptyValues()
        {
            MenuAnim.Stop(avgValue);
            MenuAnim.Stop(doneValue);
            avgValue.text = doneValue.text = growthValue.text = "–";
            growthValue.color = MenuKit.Ink;
            avgNote.text = doneNote.text = growthNote.text = "";
            ClearRows();
            recentEmpty.text = "";
            commentTitle.text = commentBody.text = "";
        }

        void Render(bool instant)
        {
            var s = StudentData.Stats;
            var list = StudentData.Results;
            // số còn đang chạy từ lần trước không được ghi đè số mới
            MenuAnim.Stop(avgValue);
            MenuAnim.Stop(doneValue);

            if (s.graded > 0)
            {
                if (instant) avgValue.text = s.average.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                else MenuAnim.Count(avgValue, s.average, "0.0", 0.1f);
                avgNote.text = $"Từ {s.graded} bài đã chấm";
            }
            else
            {
                avgValue.text = "–";
                avgNote.text = s.done > 0 ? "Giáo viên chưa chấm bài nào" : "Chưa có điểm";
            }

            if (instant) doneValue.text = s.done.ToString();
            else MenuAnim.Count(doneValue, s.done, "0", 0.14f);
            int waiting = s.done - s.graded;
            doneNote.text = waiting > 0 ? $"{waiting} bài đang chờ chấm" : s.done > 0 ? "Đã chấm hết" : "Chưa làm bài nào";

            if (s.hasGrowth)
            {
                bool up = s.growth >= 0f;
                growthValue.text = (up ? "+" : "−") + Mathf.Abs(s.growth).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                growthValue.color = up ? MenuKit.Bio : MenuKit.Error;
                growthNote.text = "3 bài gần nhất so với 3 bài trước";
            }
            else
            {
                growthValue.text = "–";
                growthValue.color = MenuKit.Ink;
                int need = StudentStats.GrowthWindow * 2 - s.graded;
                growthNote.text = $"Cần thêm {need} bài đã chấm để so sánh";
            }

            ClearRows();
            int rows = Mathf.Min(MaxRows, list.Count);
            for (int i = 0; i < rows; i++) AddRow(list[i], i, instant);
            recentEmpty.text = list.Count == 0 ? "Chưa có kết quả nào. Vào phòng thí nghiệm để làm bài đầu tiên." : "";

            StudentResult commented = null;
            foreach (var r in list)
                if (!string.IsNullOrWhiteSpace(r.nhan_xet))
                {
                    commented = r;
                    break;
                }
            commentTitle.text = commented != null ? $"{commented.ten_bai} · {commented.DateText}" : "";
            commentBody.text = commented != null ? commented.nhan_xet : "Giáo viên chưa có nhận xét nào.";
            commentBody.color = commented != null ? MenuKit.Ink : MenuKit.InkDim;
        }

        void ClearRows()
        {
            for (int i = recentList.childCount - 1; i >= 0; i--)
            {
                var c = recentList.GetChild(i);
                c.SetParent(null);
                Destroy(c.gameObject);
            }
        }

        void AddRow(StudentResult r, int index, bool instant)
        {
            var row = GlassKit.Rect(recentList, "Dòng kết quả", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -index * RowH), new Vector2(0, RowH));
            row.anchorMax = new Vector2(1, 1);
            row.offsetMin = new Vector2(0, row.offsetMin.y);
            row.offsetMax = new Vector2(0, row.offsetMax.y);
            if (index > 0)
            {
                var line = GlassKit.Rect(row, "Kẻ", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 1));
                line.anchorMax = new Vector2(1, 1);
                line.offsetMin = new Vector2(0, line.offsetMin.y);
                line.offsetMax = new Vector2(0, line.offsetMax.y);
                var li = line.gameObject.AddComponent<Image>();
                li.color = MenuKit.PaperHover;
                li.raycastTarget = false;
            }
            var name = GlassKit.Label(row, "Tên bài", r.ten_bai, 17, MenuKit.Ink, true, TextAnchor.UpperLeft, new Vector2(0, -10), new Vector2(-120, 24));
            name.rectTransform.anchorMax = new Vector2(1, 1);
            name.verticalOverflow = VerticalWrapMode.Truncate;
            GlassKit.Label(row, "Ngày", r.DateText, 14, MenuKit.InkDim, false, TextAnchor.UpperLeft, new Vector2(0, -34), new Vector2(300, 20));

            bool graded = r.Graded;
            string text = graded ? r.diem.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "Chờ chấm";
            var size = new Vector2(graded ? 56 : 96, 34);
            var chip = MenuKit.Fill(row, "Điểm", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, size, graded ? (r.diem >= 5f ? MenuKit.BioSoft : MenuKit.WarnSoft) : MenuKit.PaperAlt, MenuKit.RadiusButton);
            GlassKit.Label(chip.transform, "Chữ", text, graded ? 17 : 14, graded ? (r.diem >= 5f ? MenuKit.Bio : MenuKit.Warn) : MenuKit.InkDim, graded, TextAnchor.MiddleCenter, Vector2.zero, size);

            if (!instant) MenuAnim.Enter(row, index * 0.04f, 8f, 0.26f);
        }

        // =====================================================================
        // Thông báo, khung chờ
        // =====================================================================

        void ShowNotice(string message)
        {
            notice.text = message;
            noticeBg.color = MenuKit.WarnSoft;
            notice.color = MenuKit.Warn;
            retry.gameObject.SetActive(true);
            noticeBar.gameObject.SetActive(true);
            MenuAnim.Enter(noticeBar, 0f, 8f, 0.24f);
        }

        void HideNotice()
        {
            notice.text = "";
            noticeBar.gameObject.SetActive(false);
        }

        /// <summary>Lỗi từ MainMenu (không tải được cảnh lab...) hiện trên cùng thanh thông báo.</summary>
        public void Say(string message, bool error)
        {
            notice.text = message;
            notice.color = error ? MenuKit.Error : MenuKit.Bio;
            noticeBg.color = error ? MenuKit.WithAlpha(MenuKit.Error, 0.08f) : MenuKit.BioSoft;
            retry.gameObject.SetActive(false);
            noticeBar.gameObject.SetActive(true);
            MenuAnim.Enter(noticeBar, 0f, 8f, 0.24f);
            if (error) MenuAnim.Shake(notice.rectTransform);
        }

        /// <summary>Thanh xám nhấp nháy thay cho chữ "Đang tải". Chờ một nhịp mới hiện để máy chủ nhanh thì không chớp mắt.</summary>
        void ShowSkeleton()
        {
            if (skeleton == null)
            {
                skeleton = new GameObject("Khung chờ", typeof(RectTransform)).GetComponent<RectTransform>();
                skeleton.SetParent(MenuKit.Body(cardRecent), false);
                skeleton.anchorMin = new Vector2(0, 1);
                skeleton.anchorMax = new Vector2(1, 1);
                skeleton.pivot = new Vector2(0.5f, 1);
                skeleton.offsetMin = new Vector2(24, -60 - RowH * 3);
                skeleton.offsetMax = new Vector2(-24, -60);
                skeleton.gameObject.SetActive(false);
                for (int i = 0; i < 3; i++)
                {
                    var bar = MenuKit.Fill(skeleton, "Thanh", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -12 - i * RowH), new Vector2(0, 18), MenuKit.PaperHover, MenuKit.RadiusButton);
                    bar.rectTransform.anchorMax = new Vector2(i == 2 ? 0.45f : 0.7f, 1);
                    bar.rectTransform.offsetMin = new Vector2(0, bar.rectTransform.offsetMin.y);
                    bar.rectTransform.offsetMax = new Vector2(0, bar.rectTransform.offsetMax.y);
                    var sub = MenuKit.Fill(skeleton, "Thanh phụ", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -36 - i * RowH), new Vector2(120, 12), MenuKit.PaperAlt, MenuKit.RadiusButton);
                    sub.raycastTarget = false;
                }
            }
            skeletonShown = true;
            MenuAnim.To(skeleton, "chờ", 0.12f, _ => { }, 0f, MenuAnim.Ease.Linear, () =>
            {
                if (!skeletonShown) return;
                skeleton.gameObject.SetActive(true);
                Pulse(MenuAnim.Group(skeleton), 0f);
            });
        }

        void Pulse(CanvasGroup g, float delay)
        {
            MenuAnim.To(g, "mờ", 0.7f, k => g.alpha = Mathf.Lerp(0.45f, 1f, Mathf.Sin(k * Mathf.PI)), delay, MenuAnim.Ease.Linear, () =>
            {
                if (skeletonShown) Pulse(g, 0f);
            });
        }

        void HideSkeleton()
        {
            skeletonShown = false;
            if (skeleton == null) return;
            MenuAnim.Stop(skeleton);
            MenuAnim.Stop(MenuAnim.Group(skeleton));
            skeleton.gameObject.SetActive(false);
        }
    }
}
