using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    [Serializable]
    public sealed class BioSpecEntry
    {
        public string ten;
        public string giaTri;
        public string nguon;
        public bool moPhong;
    }

    [Serializable]
    public class BioToolRecord
    {
        public string ma;
        public string ten;
        public string tenTiengAnh;
        public string phanLoai;
        public readonly List<string> phong = new List<string>();
        public int soLuong;
        public string viTri;
        public string congDung;
        public readonly List<BioSpecEntry> thongSo = new List<BioSpecEntry>();
        public readonly List<string> cachDung = new List<string>();
        public readonly List<string> loiThuongGap = new List<string>();
        public readonly List<string> anToan = new List<string>();
        public int hieuChuanNgay;
        public string thanhPhanUnity;
        public string mayManHinh;
        public string mucDacTa;
    }

    [Serializable]
    public sealed class BioToolCatalogEntry : BioToolRecord { }

    [Serializable]
    public sealed class BioAnnotationPoint
    {
        public string ten;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class BioSlideRecord
    {
        public string ma;
        public string ten;
        public string loai;
        public string nhuom;
        public float minUm = 50f;
        public float maxUm = 200f;
        public readonly List<BioAnnotationPoint> chuThich = new List<BioAnnotationPoint>();
        public string nguon;
        public bool moPhong;
    }

    [Serializable]
    public sealed class BioStrainRecord
    {
        public string ma;
        public string tenKhoaHoc;
        public string ten;
        public string capAnToan;
        public string gram;
        public string hinhDang;
        public float thoiGianTheHePhut = 30f;
        public float nhietDoToiUuC = 37f;
        public float lagGio = 1.0f;
        public double kToiDaCfuMl = 1.0e9;
        public Color mauKhuanLac = new Color(0.95f, 0.93f, 0.85f);
        public string beMat = "bong";
        public string bo = "tron";
        public float duongKinh24hMm = 1.8f;
        public string nguon;

        public float thoi_gian_the_he_phut => thoiGianTheHePhut;
        public float lag_gio => lagGio;
        public double k_toi_da_cfu_ml => kToiDaCfuMl;
    }

    [Serializable]
    public sealed class BioMediaRecord
    {
        public string ma;
        public string ten;
        public string loai;
        public Color mau = new Color(0.95f, 0.90f, 0.72f);
        public float doTrong = 0.85f;
        public float ph = 7.0f;
        public string nguon;
    }

    [Serializable]
    public sealed class BioExperimentStep
    {
        public int so;
        public string moTa;
        public string dieuKienLoai;
        public string dieuKienMa;
        public string dieuKienVao;
        public string dieuKienTen;
        public float dieuKienGiaTri;
        public float mlMin;
        public float mlMax;
        public float cMin;
        public float cMax;
        public float gioMin;
    }

    [Serializable]
    public sealed class BioQuizItem
    {
        public string hoi;
        public string dapAn;
    }

    [Serializable]
    public class BioExperimentRecord
    {
        public string ma;
        public string ten;
        public int lop;
        public string sgk;
        public string mucTieu;
        public readonly List<string> dungCu = new List<string>();
        public readonly List<string> sinhPham = new List<string>();
        public readonly List<BioExperimentStep> buoc = new List<BioExperimentStep>();
        public string ketQuaMongDoi;
        public readonly List<BioQuizItem> cauHoi = new List<BioQuizItem>();
        public string nguon;

        string[] cachedStepTexts;
        public string muc_tieu => mucTieu;
        public string[] cac_buoc
        {
            get
            {
                if (cachedStepTexts == null || cachedStepTexts.Length != buoc.Count)
                {
                    cachedStepTexts = new string[buoc.Count];
                    for (int i = 0; i < buoc.Count; i++) cachedStepTexts[i] = buoc[i].moTa;
                }
                return cachedStepTexts;
            }
        }
    }

    [Serializable]
    public sealed class BioExperimentEntry : BioExperimentRecord { }

    [Serializable]
    public class BioReagentStockRecord
    {
        public string id;
        public string ten;
        public string loai;
        public string tu;
        public string ngan;
        public int soLuong;
        public float so_luong;
        public string donVi;
        public string soLo;
        public int hanDungNgay;
        public string capAnToan;
        public string maChung;
        public string maChat;

        public string ma { get => id; set => id = value; }
        public string don_vi => donVi;
        public string cap_an_toan => capAnToan;
    }

    [Serializable]
    public sealed class BioStockItemEntry : BioReagentStockRecord { }

    /// <summary>
    /// Bộ nạp dữ liệu sinh học Tầng 2 từ thư mục data/sinh/*.json và data/may/*.json.
    /// Cấp dữ liệu chuẩn cho thẻ thông tin EquipmentInfo, màn hình bàn học sinh, ki-ốt tra cứu và 15 bài thí nghiệm.
    /// </summary>
    public static class BioCatalogLoader
    {
        static bool loaded;
        static readonly Dictionary<string, BioToolCatalogEntry> toolsById = new Dictionary<string, BioToolCatalogEntry>();
        static readonly List<BioToolCatalogEntry> toolList = new List<BioToolCatalogEntry>();
        static readonly Dictionary<string, BioSlideRecord> slidesById = new Dictionary<string, BioSlideRecord>();
        static readonly List<BioSlideRecord> slideList = new List<BioSlideRecord>();
        static readonly Dictionary<string, BioStrainRecord> strainsById = new Dictionary<string, BioStrainRecord>();
        static readonly Dictionary<string, BioMediaRecord> mediaById = new Dictionary<string, BioMediaRecord>();
        static readonly Dictionary<string, BioExperimentEntry> experimentsById = new Dictionary<string, BioExperimentEntry>();
        static readonly List<BioExperimentEntry> experimentList = new List<BioExperimentEntry>();
        static readonly List<BioStockItemEntry> stockList = new List<BioStockItemEntry>();
        static readonly Dictionary<string, InstrumentDoc> machineDocs = new Dictionary<string, InstrumentDoc>();

        // Trạng thái an toàn sinh học toàn cục của người chơi trên Tầng 2
        public static bool HandsWashed { get; set; }
        public static bool PpeWorn { get; set; }
        public static string ActiveExperimentCode { get; set; } = "te_bao_hanh";
        public static event Action<string> OnActiveExperimentChanged;

        public static void SetActiveExperiment(string code)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(code) || !experimentsById.ContainsKey(code)) return;
            ActiveExperimentCode = code;
            OnActiveExperimentChanged?.Invoke(code);
        }

        public static IReadOnlyList<BioToolCatalogEntry> AllTools { get { EnsureLoaded(); return toolList; } }
        public static IReadOnlyList<BioToolCatalogEntry> ToolsList { get { EnsureLoaded(); return toolList; } }
        public static IReadOnlyList<BioSlideRecord> AllSlides { get { EnsureLoaded(); return slideList; } }
        public static IReadOnlyList<BioExperimentEntry> AllExperiments { get { EnsureLoaded(); return experimentList; } }
        public static IReadOnlyList<BioExperimentEntry> Experiments { get { EnsureLoaded(); return experimentList; } }
        public static IReadOnlyList<BioStockItemEntry> AllStocks { get { EnsureLoaded(); return stockList; } }
        public static IReadOnlyList<BioStockItemEntry> StockItems { get { EnsureLoaded(); return stockList; } }

        public static BioToolCatalogEntry GetTool(string ma)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(ma) && toolsById.TryGetValue(ma, out var r) ? r : null;
        }

        public static BioSlideRecord GetSlide(string ma)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(ma) && slidesById.TryGetValue(ma, out var r) ? r : null;
        }

        public static BioStrainRecord GetStrain(string ma)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(ma) && strainsById.TryGetValue(ma, out var r) ? r : null;
        }

        public static BioMediaRecord GetMedia(string ma)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(ma) && mediaById.TryGetValue(ma, out var r) ? r : null;
        }

        public static BioExperimentEntry GetExperiment(string ma)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(ma) && experimentsById.TryGetValue(ma, out var r) ? r : (experimentList.Count > 0 ? experimentList[0] : null);
        }

        public static InstrumentDoc LoadMachineDoc(string code)
        {
            if (string.IsNullOrEmpty(code)) return new InstrumentDoc { Code = "", Name = "" };
            if (machineDocs.TryGetValue(code, out var cached)) return cached;

            string json = ReadJsonFile($"data/may/{code}.json") ?? ReadJsonFile($"Assets/Lab/Data/May/{code}.json");
            InstrumentDoc doc = !string.IsNullOrEmpty(json)
                ? InstrumentDoc.Parse(json)
                : new InstrumentDoc { Code = code, Name = code, Model = "VERA Lab Bio" };
            machineDocs[code] = doc;
            return doc;
        }

        /// <summary>
        /// Gắn thẻ thông tin EquipmentInfo và BioToolCatalogTag cho một đối tượng dụng cụ theo mã trong dung_cu.json.
        /// </summary>
        public static EquipmentInfo AttachToolInfo(GameObject go, string ma, string roomOverride = "")
        {
            if (go == null || string.IsNullOrEmpty(ma)) return null;
            EnsureLoaded();

            var tag = go.GetComponent<BioToolCatalogTag>();
            if (tag == null) tag = go.AddComponent<BioToolCatalogTag>();
            tag.toolCode = ma;
            if (!string.IsNullOrEmpty(roomOverride)) tag.roomCode = roomOverride;

            var info = go.GetComponent<EquipmentInfo>();
            if (info == null) info = go.AddComponent<EquipmentInfo>();

            if (toolsById.TryGetValue(ma, out var rec))
            {
                if (string.IsNullOrEmpty(tag.roomCode) && rec.phong.Count > 0)
                    tag.roomCode = rec.phong[0];

                info.title = rec.ten;

                var sbDesc = new StringBuilder();
                sbDesc.Append(rec.congDung);
                if (rec.thongSo.Count > 0)
                {
                    sbDesc.Append("\n• Thông số: ");
                    int n = Mathf.Min(2, rec.thongSo.Count);
                    for (int i = 0; i < n; i++)
                    {
                        if (i > 0) sbDesc.Append("; ");
                        sbDesc.Append(rec.thongSo[i].ten).Append(": ").Append(rec.thongSo[i].giaTri);
                    }
                }
                info.description = sbDesc.ToString();

                var sbUse = new StringBuilder();
                if (rec.cachDung.Count > 0)
                    sbUse.Append("Cách dùng: ").Append(string.Join(" ", rec.cachDung));
                if (rec.loiThuongGap.Count > 0)
                    sbUse.Append("\nLỗi thường gặp: ").Append(rec.loiThuongGap[0]);
                if (rec.anToan.Count > 0)
                    sbUse.Append("\nAn toàn: ").Append(rec.anToan[0]);
                info.usage = sbUse.ToString();
            }
            else if (string.IsNullOrEmpty(info.title))
            {
                info.title = ma;
                info.description = "Dụng cụ thực hành Sinh học Tầng 2.";
                info.usage = "Bấm chuột trái hoặc cầm dụng cụ để thao tác.";
            }

            return info;
        }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            LoadTools();
            LoadSlides();
            LoadStrains();
            LoadMedia();
            LoadExperiments();
            LoadStocks();
        }

        static string ReadJsonFile(string relativePath)
        {
            try
            {
                // Thử từ thư mục gốc kho git (../data/sinh/...) hoặc trong Assets/
                string p1 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", relativePath));
                if (File.Exists(p1)) return File.ReadAllText(p1, Encoding.UTF8);

                string p2 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                if (File.Exists(p2)) return File.ReadAllText(p2, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BioCatalogLoader] Không đọc được {relativePath}: {e.Message}");
            }
            return null;
        }

        static void LoadTools()
        {
            toolsById.Clear();
            toolList.Clear();
            string json = ReadJsonFile("data/sinh/dung_cu.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "dung_cu"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                var rec = new BioToolCatalogEntry
                {
                    ma = MiniJson.S(d, "ma"),
                    ten = MiniJson.S(d, "ten"),
                    tenTiengAnh = MiniJson.S(d, "ten_tieng_anh"),
                    phanLoai = MiniJson.S(d, "phan_loai"),
                    soLuong = (int)MiniJson.N(d, "so_luong", 1),
                    viTri = MiniJson.S(d, "vi_tri"),
                    congDung = MiniJson.S(d, "cong_dung"),
                    hieuChuanNgay = (int)MiniJson.N(d, "hieu_chuan_ngay", 0),
                    thanhPhanUnity = MiniJson.S(d, "thanh_phan_unity"),
                    mayManHinh = MiniJson.S(d, "may_man_hinh"),
                    mucDacTa = MiniJson.S(d, "muc_dac_ta")
                };
                foreach (var p in MiniJson.A(d, "phong")) if (p is string ps) rec.phong.Add(ps);
                foreach (var c in MiniJson.A(d, "cach_dung")) if (c is string cs) rec.cachDung.Add(cs);
                foreach (var l in MiniJson.A(d, "loi_thuong_gap")) if (l is string ls) rec.loiThuongGap.Add(ls);
                foreach (var a in MiniJson.A(d, "an_toan")) if (a is string ans) rec.anToan.Add(ans);
                foreach (var ts in MiniJson.A(d, "thong_so"))
                {
                    var tso = MiniJson.Obj(ts);
                    if (tso == null) continue;
                    rec.thongSo.Add(new BioSpecEntry
                    {
                        ten = MiniJson.S(tso, "ten"),
                        giaTri = MiniJson.S(tso, "gia_tri"),
                        nguon = MiniJson.S(tso, "nguon"),
                        moPhong = MiniJson.B(tso, "mo_phong")
                    });
                }
                if (!string.IsNullOrEmpty(rec.ma))
                {
                    toolsById[rec.ma] = rec;
                    toolList.Add(rec);
                }
            }
        }

        static void LoadSlides()
        {
            slidesById.Clear();
            slideList.Clear();
            string json = ReadJsonFile("data/sinh/tieu_ban.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "tieu_ban"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                var rec = new BioSlideRecord
                {
                    ma = MiniJson.S(d, "ma"),
                    ten = MiniJson.S(d, "ten"),
                    loai = MiniJson.S(d, "loai"),
                    nhuom = MiniJson.S(d, "nhuom"),
                    nguon = MiniJson.S(d, "nguon"),
                    moPhong = MiniJson.B(d, "mo_phong")
                };
                var sz = MiniJson.A(d, "kich_thuoc_te_bao_um");
                if (sz.Count >= 2)
                {
                    rec.minUm = Convert.ToSingle(sz[0]);
                    rec.maxUm = Convert.ToSingle(sz[1]);
                }
                foreach (var bp in MiniJson.A(d, "bo_phan_chu_thich"))
                {
                    var bpo = MiniJson.Obj(bp);
                    if (bpo == null) continue;
                    rec.chuThich.Add(new BioAnnotationPoint
                    {
                        ten = MiniJson.S(bpo, "ten"),
                        x = (float)MiniJson.N(bpo, "x", 0.5),
                        y = (float)MiniJson.N(bpo, "y", 0.5)
                    });
                }
                if (!string.IsNullOrEmpty(rec.ma))
                {
                    slidesById[rec.ma] = rec;
                    slideList.Add(rec);
                }
            }
        }

        static void LoadStrains()
        {
            strainsById.Clear();
            string json = ReadJsonFile("data/sinh/chung_vi_sinh.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "chung_vi_sinh"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                var rec = new BioStrainRecord
                {
                    ma = MiniJson.S(d, "ma"),
                    tenKhoaHoc = MiniJson.S(d, "ten_khoa_hoc"),
                    ten = MiniJson.S(d, "ten"),
                    capAnToan = MiniJson.S(d, "cap_an_toan"),
                    gram = MiniJson.S(d, "gram"),
                    hinhDang = MiniJson.S(d, "hinh_dang"),
                    thoiGianTheHePhut = (float)MiniJson.N(d, "thoi_gian_the_he_phut", 30.0),
                    nhietDoToiUuC = (float)MiniJson.N(d, "nhiet_do_toi_uu_c", 37.0),
                    nguon = MiniJson.S(d, "nguon")
                };
                var kl = MiniJson.Obj(d.TryGetValue("khuan_lac", out var klo) ? klo : null);
                if (kl != null)
                {
                    rec.beMat = MiniJson.S(kl, "be_mat");
                    rec.bo = MiniJson.S(kl, "bo");
                    rec.duongKinh24hMm = (float)MiniJson.N(kl, "duong_kinh_24h_mm", 1.8);
                    var rgb = MiniJson.A(kl, "mau");
                    if (rgb.Count >= 3)
                    {
                        rec.mauKhuanLac = new Color(
                            Convert.ToSingle(rgb[0]) / 255f,
                            Convert.ToSingle(rgb[1]) / 255f,
                            Convert.ToSingle(rgb[2]) / 255f);
                    }
                }
                if (!string.IsNullOrEmpty(rec.ma))
                    strainsById[rec.ma] = rec;
            }
        }

        static void LoadMedia()
        {
            mediaById.Clear();
            string json = ReadJsonFile("data/sinh/moi_truong.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "moi_truong"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                var rec = new BioMediaRecord
                {
                    ma = MiniJson.S(d, "ma"),
                    ten = MiniJson.S(d, "ten"),
                    loai = MiniJson.S(d, "loai"),
                    doTrong = (float)MiniJson.N(d, "do_trong", 0.85),
                    ph = (float)MiniJson.N(d, "ph", 7.0),
                    nguon = MiniJson.S(d, "nguon")
                };
                var rgb = MiniJson.A(d, "mau");
                if (rgb.Count >= 3)
                {
                    rec.mau = new Color(
                        Convert.ToSingle(rgb[0]) / 255f,
                        Convert.ToSingle(rgb[1]) / 255f,
                        Convert.ToSingle(rgb[2]) / 255f,
                        rec.doTrong);
                }
                if (!string.IsNullOrEmpty(rec.ma))
                    mediaById[rec.ma] = rec;
            }
        }

        static void LoadExperiments()
        {
            experimentsById.Clear();
            experimentList.Clear();
            string json = ReadJsonFile("data/sinh/bai_thi_nghiem.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "bai_thi_nghiem"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                var rec = new BioExperimentEntry
                {
                    ma = MiniJson.S(d, "ma"),
                    ten = MiniJson.S(d, "ten"),
                    lop = (int)MiniJson.N(d, "lop", 10),
                    sgk = MiniJson.S(d, "sgk"),
                    mucTieu = MiniJson.S(d, "muc_tieu"),
                    ketQuaMongDoi = MiniJson.S(d, "ket_qua_mong_doi"),
                    nguon = MiniJson.S(d, "nguon")
                };
                foreach (var dc in MiniJson.A(d, "dung_cu")) if (dc is string dcs) rec.dungCu.Add(dcs);
                foreach (var sp in MiniJson.A(d, "sinh_pham")) if (sp is string sps) rec.sinhPham.Add(sps);
                foreach (var b in MiniJson.A(d, "buoc"))
                {
                    var bo = MiniJson.Obj(b);
                    if (bo == null) continue;
                    var st = new BioExperimentStep
                    {
                        so = (int)MiniJson.N(bo, "so", 1),
                        moTa = MiniJson.S(bo, "mo_ta")
                    };
                    if (bo.TryGetValue("dieu_kien", out var dko))
                    {
                        var dk = MiniJson.Obj(dko);
                        if (dk != null)
                        {
                            st.dieuKienLoai = MiniJson.S(dk, "loai");
                            st.dieuKienMa = MiniJson.S(dk, "ma");
                            st.dieuKienVao = MiniJson.S(dk, "vao");
                            st.dieuKienTen = MiniJson.S(dk, "ten");
                            st.dieuKienGiaTri = (float)MiniJson.N(dk, "gia_tri", 0);
                            st.mlMin = (float)MiniJson.N(dk, "ml_min", 0);
                            st.mlMax = (float)MiniJson.N(dk, "ml_max", 0);
                            st.cMin = (float)MiniJson.N(dk, "c_min", 0);
                            st.cMax = (float)MiniJson.N(dk, "c_max", 0);
                            st.gioMin = (float)MiniJson.N(dk, "gio_min", 0);
                        }
                    }
                    rec.buoc.Add(st);
                }
                foreach (var ch in MiniJson.A(d, "cau_hoi"))
                {
                    var cho = MiniJson.Obj(ch);
                    if (cho == null) continue;
                    rec.cauHoi.Add(new BioQuizItem
                    {
                        hoi = MiniJson.S(cho, "hoi"),
                        dapAn = MiniJson.S(cho, "dap_an")
                    });
                }
                if (!string.IsNullOrEmpty(rec.ma))
                {
                    experimentsById[rec.ma] = rec;
                    experimentList.Add(rec);
                }
            }
        }

        static void LoadStocks()
        {
            stockList.Clear();
            string json = ReadJsonFile("data/sinh/sinh_pham_mac_dinh.json");
            if (string.IsNullOrEmpty(json)) return;

            var root = MiniJson.Obj(MiniJson.Parse(json));
            foreach (var item in MiniJson.A(root, "sinh_pham"))
            {
                var d = MiniJson.Obj(item);
                if (d == null) continue;
                stockList.Add(new BioStockItemEntry
                {
                    id = MiniJson.S(d, "id"),
                    ten = MiniJson.S(d, "ten"),
                    loai = MiniJson.S(d, "loai"),
                    tu = MiniJson.S(d, "tu"),
                    ngan = MiniJson.S(d, "ngan"),
                    soLuong = (int)MiniJson.N(d, "so_luong", 1),
                    donVi = MiniJson.S(d, "don_vi"),
                    soLo = MiniJson.S(d, "so_lo"),
                    hanDungNgay = (int)MiniJson.N(d, "han_dung_ngay", 30),
                    capAnToan = MiniJson.S(d, "cap_an_toan"),
                    maChung = MiniJson.S(d, "ma_chung"),
                    maChat = MiniJson.S(d, "ma_chat")
                });
            }
        }
    }
}

