using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Bảng "Câu hỏi nhanh": 9 câu mỗi mô phỏng (cau-hoi.json của CHEMISTRY), ba dạng I (4 lựa chọn), II (đúng/sai 4 ý), III (trả lời ngắn).
    /// Các ô dựng sẵn một lần, đổi câu chỉ đổi chữ và ẩn/hiện.
    /// </summary>
    public sealed class BangCauHoi
    {
        readonly RectTransform goc;
        readonly Text dong1, diem, tieuDe, noiDung, ketQua, giaiThich;
        readonly Button[] luaChon = new Button[4];
        readonly Text[] yChu = new Text[4];
        readonly Button[] yDung = new Button[4], ySai = new Button[4];
        readonly Text[] yDau = new Text[4];
        readonly InputField oSo;
        readonly Button nutKiemTra, nutTruoc, nutSau;
        readonly Dictionary<string, List<CauHoi>> boNho = new Dictionary<string, List<CauHoi>>();

        List<CauHoi> ds = new List<CauHoi>();
        bool[] daTraLoi = new bool[0], dungCau = new bool[0];
        readonly int[] yChon = new int[4];   // −1 chưa chọn, 1 đúng, 0 sai
        public int ChiSo { get; private set; }
        public CauHoi Cau => ds.Count > 0 ? ds[ChiSo] : null;
        public int SoCau => ds.Count;
        public int SoDung { get { int s = 0; foreach (var d in dungCau) if (d) s++; return s; } }
        /// <summary>Kết quả câu hiện tại: null chưa trả lời, true đúng, false sai.</summary>
        public bool? KetQua => ds.Count == 0 || !daTraLoi[ChiSo] ? (bool?)null : dungCau[ChiSo];
        public string LoiNap { get; private set; } = "";

        public BangCauHoi(RectTransform cha)
        {
            goc = cha;
            float W = 560;
            dong1 = HoaLyUi.Chu(goc, "Số câu", "", 13, GlassKit.Accent2, true, TextAnchor.UpperLeft, new Vector2(20, -8), new Vector2(360, 18));
            diem = HoaLyUi.Chu(goc, "Điểm", "", 13, GlassKit.Dim, true, TextAnchor.UpperRight, new Vector2(300, -8), new Vector2(280, 18));
            tieuDe = HoaLyUi.Chu(goc, "Tiêu đề câu", "", 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(20, -32), new Vector2(W, 46));
            noiDung = HoaLyUi.Chu(goc, "Nội dung câu", "", 15, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(20, -84), new Vector2(W, 130));
            for (int i = 0; i < 4; i++)
            {
                int ii = i;
                luaChon[i] = GlassKit.MakeButton(goc, "Lựa chọn " + i, "", new Vector2(20, -222 - i * 64), new Vector2(W, 56), () => ChonLuaChon(ii), false, 14);
                var t = luaChon[i].transform.Find("Chữ").GetComponent<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.anchoredPosition = new Vector2(16, 0);
                t.rectTransform.sizeDelta = new Vector2(W - 32, 56);

                float y = -222 - i * 72;
                yChu[i] = HoaLyUi.Chu(goc, "Ý " + i, "", 14, GlassKit.Ink, false, TextAnchor.UpperLeft, new Vector2(20, y), new Vector2(372, 68));
                yDung[i] = GlassKit.MakeChip(goc, "Ý đúng " + i, "Đúng", new Vector2(400, y), new Vector2(72, 38), () => ChonY(ii, true), false, 13);
                ySai[i] = GlassKit.MakeChip(goc, "Ý sai " + i, "Sai", new Vector2(478, y), new Vector2(64, 38), () => ChonY(ii, false), false, 13);
                yDau[i] = HoaLyUi.Chu(goc, "Dấu ý " + i, "", 16, GlassKit.Good, true, TextAnchor.UpperCenter, new Vector2(544, y - 6), new Vector2(30, 24));
            }
            oSo = GlassKit.MakeInput(goc, "Ô đáp số", "Nhập số, ví dụ 12,47", new Vector2(20, -224), new Vector2(380, 50), 16);
            oSo.onSubmit.AddListener(_ => KiemTraSo());
            nutKiemTra = GlassKit.MakeButton(goc, "Kiểm tra", "Kiểm tra", new Vector2(412, -224), new Vector2(168, 50), KiemTraSo, true, 15);
            ketQua = HoaLyUi.Chu(goc, "Kết quả", "", 16, GlassKit.Good, true, TextAnchor.UpperLeft, new Vector2(20, -520), new Vector2(W, 24));
            giaiThich = HoaLyUi.Chu(goc, "Giải thích", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -550), new Vector2(W, 230));
            nutTruoc = GlassKit.MakeButton(goc, "Câu trước", "‹ Câu trước", new Vector2(20, 66), new Vector2(170, 46), () => Hien(ChiSo - 1), false, 15, new Vector2(0, 0));
            nutSau = GlassKit.MakeButton(goc, "Câu sau", "Câu sau ›", new Vector2(410, 66), new Vector2(170, 46), () => Hien(ChiSo + 1), true, 15, new Vector2(0, 0));
        }

        /// <summary>Nạp bộ câu hỏi (Resources/HoaLy/&lt;tep&gt;.json), giữ trong bộ nhớ đệm.</summary>
        public void Nap(string tep)
        {
            if (!boNho.TryGetValue(tep, out var l))
            {
                l = new List<CauHoi>();
                var ta = Resources.Load<TextAsset>("HoaLy/" + tep);
                if (ta == null) LoiNap = "Thiếu Resources/HoaLy/" + tep + ".json";
                else
                {
                    try { l = CauHoiHoaLy.Doc(ta.text); LoiNap = ""; }
                    catch (System.Exception e) { LoiNap = "Lỗi đọc " + tep + ": " + e.Message; }
                }
                boNho[tep] = l;
            }
            if (ReferenceEquals(ds, l)) return;
            ds = l;
            daTraLoi = new bool[ds.Count];
            dungCau = new bool[ds.Count];
            Hien(0);
        }

        public void Hien(int i)
        {
            if (ds.Count == 0)
            {
                tieuDe.text = LoiNap.Length > 0 ? LoiNap : "Chưa có câu hỏi.";
                return;
            }
            ChiSo = Mathf.Clamp(i, 0, ds.Count - 1);
            var c = ds[ChiSo];
            string dang = c.Dang == DangCauHoi.TracNghiem ? "Trắc nghiệm" : c.Dang == DangCauHoi.DungSai ? "Đúng / Sai" : "Trả lời ngắn";
            dong1.text = $"CÂU {ChiSo + 1}/{ds.Count} · {dang.ToUpperInvariant()}";
            tieuDe.text = BoTienTo(c.TieuDe);
            noiDung.text = c.NoiDung;
            for (int k = 0; k < 4; k++)
            {
                bool coLc = c.Dang == DangCauHoi.TracNghiem && k < c.LuaChon.Length;
                luaChon[k].gameObject.SetActive(coLc);
                if (coLc)
                {
                    luaChon[k].transform.Find("Chữ").GetComponent<Text>().text = c.LuaChon[k];
                    GlassKit.SetChip(luaChon[k], false);
                }
                bool coY = c.Dang == DangCauHoi.DungSai && k < c.Y.Length;
                yChu[k].gameObject.SetActive(coY); yDung[k].gameObject.SetActive(coY); ySai[k].gameObject.SetActive(coY); yDau[k].gameObject.SetActive(coY);
                if (coY)
                {
                    yChu[k].text = c.Y[k].NoiDung;
                    GlassKit.SetChip(yDung[k], false); GlassKit.SetChip(ySai[k], false);
                    yDau[k].text = "";
                }
                yChon[k] = -1;
            }
            bool so = c.Dang == DangCauHoi.TraLoiNgan;
            oSo.gameObject.SetActive(so); nutKiemTra.gameObject.SetActive(so);
            oSo.text = "";
            ketQua.text = ""; giaiThich.text = "";
            nutTruoc.interactable = ChiSo > 0;
            nutSau.interactable = ChiSo < ds.Count - 1;
            CapNhatDiem();
        }

        static string BoTienTo(string s)
        {
            // "Câu 1 (Phần I): Định luật Boyle" → "Định luật Boyle"
            int i = s.IndexOf("): ", System.StringComparison.Ordinal);
            return i > 0 && i < 30 ? s.Substring(i + 3) : s;
        }

        void CapNhatDiem() => diem.text = $"Đúng {SoDung}/{ds.Count}";

        void Cham(bool dung, string chu)
        {
            daTraLoi[ChiSo] = true;
            dungCau[ChiSo] = dung;
            ketQua.color = dung ? GlassKit.Good : GlassKit.Bad;
            ketQua.text = chu;
            var c = ds[ChiSo];
            giaiThich.text = c.GiaiThich + (string.IsNullOrEmpty(c.Nguon) ? "" : "\n<i>Nguồn: " + c.Nguon + "</i>");
            CapNhatDiem();
        }

        public void ChonLuaChon(int k)
        {
            var c = Cau;
            if (c == null || c.Dang != DangCauHoi.TracNghiem) return;
            int dung = c.ChiSoDapAn;
            for (int j = 0; j < 4; j++) GlassKit.SetChip(luaChon[j], j == k || j == dung);
            var img = (Image)luaChon[k].targetGraphic;
            if (k != dung) img.color = GlassKit.BadSoft;
            if (dung >= 0 && dung < 4) ((Image)luaChon[dung].targetGraphic).color = GlassKit.GoodSoft;
            Cham(k == dung, k == dung ? "✓ Chính xác!" : "✗ Chưa đúng – đáp án " + c.DapAn);
        }

        public void ChonY(int k, bool dung)
        {
            var c = Cau;
            if (c == null || c.Dang != DangCauHoi.DungSai || k >= c.Y.Length) return;
            yChon[k] = dung ? 1 : 0;
            GlassKit.SetChip(yDung[k], dung); GlassKit.SetChip(ySai[k], !dung);
            for (int j = 0; j < c.Y.Length; j++) if (yChon[j] < 0) return;
            int soDung = 0;
            for (int j = 0; j < c.Y.Length; j++)
            {
                bool ok = (yChon[j] == 1) == c.Y[j].Dung;
                if (ok) soDung++;
                yDau[j].color = ok ? GlassKit.Good : GlassKit.Bad;
                yDau[j].text = ok ? "✓" : "✗";
            }
            Cham(soDung == c.Y.Length, soDung == c.Y.Length ? "✓ Đúng cả " + soDung + " ý!" : $"Đúng {soDung}/{c.Y.Length} ý");
        }

        public void NhapSo(string s) { oSo.text = s; KiemTraSo(); }

        void KiemTraSo()
        {
            var c = Cau;
            if (c == null || c.Dang != DangCauHoi.TraLoiNgan || string.IsNullOrWhiteSpace(oSo.text)) return;
            bool ok = CauHoiHoaLy.KiemTraTraLoiNgan(oSo.text, c.DapSo);
            Cham(ok, ok ? "✓ Chính xác: " + c.DapSo : "✗ Chưa đúng – đáp số " + c.DapSo);
        }
    }
}
