using System;
using System.Collections.Generic;
using UnityEngine;
using PhongThiNghiem;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.NhanVat;

namespace PhongThiNghiem.ThiNghiem
{
    [Serializable]
    public class MoHinhItem
    {
        public string vaiTro;
        public GameObject prefab;
        public Vector3 scaleChuan = Vector3.one;
        public Vector3 offsetViTri = Vector3.zero;
        public Vector3 offsetGocXoay = Vector3.zero;
    }

    /// <summary>
    /// KhoMoHinh: Quản lý mô hình 3D cho toàn bộ phòng lab theo vai_tro
    /// (Tương thích ScriptableObject / MonoBehaviour Singleton runtime).
    /// Hỗ trợ nạp prefab từ Assets/TaiNguyenNgoai/Sketchfab hoặc Assets/Prefabs.
    /// QUY TẮC BẮT BUỘC (PROMPT_DUNG_3D_TU_SKETCHFAB.md):
    /// Khi chưa có prefab tải về từ Sketchfab, hệ thống tự động fallback
    /// sang các mô hình procedural PBR chuẩn ISO quốc tế với tỷ lệ chính xác.
    /// </summary>
    public class KhoMoHinh : MonoBehaviour
    {
        public static KhoMoHinh Instance { get; set; }

        public List<MoHinhItem> DanhSachMoHinh = new List<MoHinhItem>();
        private readonly Dictionary<string, MoHinhItem> _traCuu = new Dictionary<string, MoHinhItem>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                KhoiTaoTraCuu();
            }
        }

        public void KhoiTaoTraCuu()
        {
            _traCuu.Clear();
            if (DanhSachMoHinh != null)
            {
                foreach (var item in DanhSachMoHinh)
                {
                    if (!string.IsNullOrEmpty(item.vaiTro))
                    {
                        _traCuu[item.vaiTro] = item;
                    }
                }
            }
        }

        public bool CoPrefab(string vaiTro)
        {
            return _traCuu.TryGetValue(vaiTro, out var item) && item != null && item.prefab != null;
        }

        public void DangKyMoHinh(string vaiTro, GameObject prefab, Vector3? scale = null, Vector3? offsetPos = null)
        {
            if (string.IsNullOrEmpty(vaiTro)) return;
            var item = new MoHinhItem
            {
                vaiTro = vaiTro,
                prefab = prefab,
                scaleChuan = scale ?? Vector3.one,
                offsetViTri = offsetPos ?? Vector3.zero
            };
            _traCuu[vaiTro] = item;
            int idx = DanhSachMoHinh.FindIndex(m => m.vaiTro == vaiTro);
            if (idx >= 0) DanhSachMoHinh[idx] = item;
            else DanhSachMoHinh.Add(item);
        }

        /// <summary>
        /// Tạo vật thể trong phòng hoặc trên bàn thí nghiệm:
        /// Ưu tiên dùng prefab đã đăng ký từ Sketchfab.
        /// Nếu không có, tự động chuyển sang procedural ISO fallback.
        /// </summary>
        public GameObject TaoVatThe(string vaiTro, Transform parent = null, Vector3 localPosition = default, Quaternion localRotation = default, Vector3? scale = null)
        {
            if (_traCuu.TryGetValue(vaiTro, out var item) && item != null && item.prefab != null)
            {
                var go = Instantiate(item.prefab, parent);
                go.name = "Model_" + vaiTro;
                go.transform.localPosition = localPosition + item.offsetViTri;
                go.transform.localRotation = localRotation * Quaternion.Euler(item.offsetGocXoay);
                go.transform.localScale = scale.HasValue ? Vector3.Scale(scale.Value, item.scaleChuan) : item.scaleChuan;

                if (go.GetComponent<Collider>() == null)
                {
                    go.AddComponent<BoxCollider>();
                }
                return go;
            }

            return TaoFallbackProcedural(vaiTro, parent, localPosition, localRotation, scale);
        }

        public GameObject TaoFallbackProcedural(string vaiTro, Transform parent = null, Vector3 pos = default, Quaternion rot = default, Vector3? scale = null)
        {
            GameObject obj = null;
            switch (vaiTro)
            {
                case "ban_chinh":
                    obj = TaoBanChinhProcedural();
                    break;
                case "ban_chau":
                    obj = TaoBanChauProcedural();
                    break;
                case "ban_tuong":
                    obj = TaoBanTuongProcedural();
                    break;
                case "ghe":
                    obj = TaoGheLabProcedural();
                    break;
                case "tu_hut":
                    obj = TaoTuHutProcedural();
                    break;
                case "an_toan":
                case "tram_rua_mat":
                    obj = TaoTramRuaMatProcedural();
                    break;
                case "binh_chua_chay":
                    obj = TaoBinhChuaChayProcedural();
                    break;
                case "burette":
                    obj = DungCuISO.TaoBurette50mL(null, Vector3.zero);
                    break;
                case "binh_tam_giac":
                    obj = DungCuISO.TaoBinhTamGiac250mL(null, Vector3.zero);
                    break;
                case "ong_nghiem":
                case "coc_dong":
                    obj = DungCuISO.TaoCocDot250mL(null, Vector3.zero);
                    break;
                case "gia_ong_nghiem":
                    obj = DungCuISO.TaoGiaOngNghiem6Lo(null, Vector3.zero);
                    break;
                case "den_bunsen":
                    obj = TaoDenBunsenProcedural();
                    break;
                case "ngon_lua":
                    obj = TaoNgonLuaProcedural();
                    break;
                case "dien_hoa":
                case "dong_ho_van_nang":
                    obj = TaoDongHoVanNangProcedural();
                    break;
                case "nguon_dc":
                    obj = TaoNguonDCProcedural();
                    break;
                case "nhan_vat":
                case "nha_nghien_cuu":
                    var goNv = new GameObject("Fallback_NhanVat");
                    goNv.AddComponent<DungNhanVat>();
                    obj = goNv;
                    break;
                default:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Fallback_" + vaiTro;
                    obj.transform.localScale = scale ?? new Vector3(0.3f, 0.3f, 0.3f);
                    break;
            }

            if (obj != null)
            {
                if (parent != null) obj.transform.SetParent(parent, false);
                obj.transform.localPosition = pos;
                obj.transform.localRotation = rot;
                if (scale.HasValue) obj.transform.localScale = scale.Value;

                if (obj.GetComponent<Collider>() == null && obj.GetComponentInChildren<Collider>() == null)
                {
                    obj.AddComponent<BoxCollider>();
                }
            }
            return obj;
        }

        #region Procedural Model Generators (Nâng cấp chi tiết theo kho-mo-hinh-3d.html)

        public static GameObject TaoBanChinhProcedural()
        {
            var ban = new GameObject("BanThiNghiem_Chinh");
            // Kích thước chuẩn: rộng 2.4m, cao mặt 0.90m, sâu 1.2m
            float w = 2.4f, h = 0.90f, d = 1.2f;

            // Khung thép sơn xám tĩnh điện
            var khung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            khung.name = "KhungThep";
            khung.transform.SetParent(ban.transform, false);
            khung.transform.localPosition = new Vector3(0f, (h - 0.04f) * 0.5f, 0f);
            khung.transform.localScale = new Vector3(w * 0.96f, h - 0.04f, d * 0.94f);
            khung.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.35f, 0.38f, 0.42f));

            // Tủ ngăn kéo gỗ sồi sáng
            var tu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tu.name = "TuGoSoi";
            tu.transform.SetParent(ban.transform, false);
            tu.transform.localPosition = new Vector3(0f, (h - 0.04f) * 0.5f, 0f);
            tu.transform.localScale = new Vector3(w * 0.90f, h - 0.06f, d * 0.86f);
            tu.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.72f, 0.58f, 0.42f), 0.3f);

            // Mặt bàn Phenolic resin xanh navy chịu hoá chất dày 40mm
            var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "MatPhenolic";
            mat.transform.SetParent(ban.transform, false);
            mat.transform.localPosition = new Vector3(0f, h - 0.02f, 0f);
            mat.transform.localScale = new Vector3(w, 0.04f, d);
            mat.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.06f, 0.16f, 0.28f), 0.45f);

            var col = ban.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);

            return ban;
        }

        public static GameObject TaoBanChauProcedural()
        {
            var ban = new GameObject("BanThiNghiem_CoChau");
            float w = 1.8f, h = 0.90f, d = 0.9f;

            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "ThanBan";
            than.transform.SetParent(ban.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(w, h, d);
            than.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.85f, 0.87f, 0.89f), 0.25f);

            // Chậu rửa PP chịu acid
            var chau = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chau.name = "ChauRuaPP";
            chau.transform.SetParent(ban.transform, false);
            chau.transform.localPosition = new Vector3(w * 0.28f, h - 0.05f, 0f);
            chau.transform.localScale = new Vector3(0.50f, 0.25f, 0.42f);
            chau.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.20f, 0.22f, 0.25f), 0.15f);

            // Vòi nước 3 nhánh phòng lab
            var voi = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            voi.name = "VoiNuocLab";
            voi.transform.SetParent(ban.transform, false);
            voi.transform.localPosition = new Vector3(w * 0.28f, h + 0.15f, d * 0.32f);
            voi.transform.localScale = new Vector3(0.02f, 0.15f, 0.02f);
            voi.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.92f, 0.92f, 0.95f));

            var col = ban.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);

            return ban;
        }

        public static GameObject TaoBanTuongProcedural()
        {
            var ban = new GameObject("BanTuongLab");
            float w = 2.0f, h = 0.90f, d = 0.75f;

            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "ThanBanTuong";
            than.transform.SetParent(ban.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(w, h, d);
            than.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.78f, 0.80f, 0.83f), 0.25f);

            var col = ban.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);

            return ban;
        }

        public static GameObject TaoGheLabProcedural()
        {
            var ghe = new GameObject("GheLab");
            float r = 0.18f, h = 0.65f;

            // Mặt ghế tròn xoay
            var matGhe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            matGhe.name = "MatGhe";
            matGhe.transform.SetParent(ghe.transform, false);
            matGhe.transform.localPosition = new Vector3(0f, h, 0f);
            matGhe.transform.localScale = new Vector3(r * 2f, 0.03f, r * 2f);
            matGhe.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.12f, 0.20f, 0.32f), 0.3f);

            // Trụ ben hơi inox
            var tru = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tru.name = "TruBenHoi";
            tru.transform.SetParent(ghe.transform, false);
            tru.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            tru.transform.localScale = new Vector3(0.04f, h * 0.5f, 0.04f);
            tru.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.88f, 0.88f, 0.90f));

            // Chân 5 sao
            var chan = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chan.name = "Chan5Sao";
            chan.transform.SetParent(ghe.transform, false);
            chan.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            chan.transform.localScale = new Vector3(0.48f, 0.04f, 0.48f);
            chan.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.25f, 0.27f, 0.30f));

            var col = ghe.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(0.5f, h, 0.5f);

            return ghe;
        }

        public static GameObject TaoTuHutProcedural()
        {
            var tu = new GameObject("TuHutKhiDoc_LySonSaKy");
            float w = 1.5f, h = 2.30f, d = 0.90f;

            // Thân tủ chính
            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "VoTu";
            than.transform.SetParent(tu.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(w, h, d);
            than.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.85f, 0.86f, 0.88f));

            // Khoang làm việc (mặt bàn phenolic ceramic)
            var matLamViec = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matLamViec.name = "MatLamViecCeramic";
            matLamViec.transform.SetParent(tu.transform, false);
            matLamViec.transform.localPosition = new Vector3(0f, 0.90f, 0.05f);
            matLamViec.transform.localScale = new Vector3(w * 0.88f, 0.04f, d * 0.82f);
            matLamViec.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.20f, 0.22f, 0.25f), 0.4f);

            // Cửa trượt kính cường lực chống nổ
            var cuaKinh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuaKinh.name = "CuaKinhTruot";
            cuaKinh.transform.SetParent(tu.transform, false);
            cuaKinh.transform.localPosition = new Vector3(0f, 1.45f, d * 0.46f);
            cuaKinh.transform.localScale = new Vector3(w * 0.85f, 0.85f, 0.015f);
            cuaKinh.GetComponent<Renderer>().material = LabVatLieu.ThuyTinh();

            // Đèn LED chiếu sáng khoang tủ
            var goLight = new GameObject("DenLEDKhoangTu");
            goLight.transform.SetParent(tu.transform, false);
            goLight.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            var l = goLight.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 3.5f;
            l.intensity = 1.8f;
            l.color = new Color(0.95f, 0.98f, 1.0f);

            var col = tu.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);

            return tu;
        }

        public static GameObject TaoTramRuaMatProcedural()
        {
            var tram = new GameObject("TramRuaMat_KhanCap");
            float h = 2.20f;

            // Trụ đứng ống thép sơn vàng an toàn ANSI
            var tru = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tru.name = "TruThepVang";
            tru.transform.SetParent(tram.transform, false);
            tru.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            tru.transform.localScale = new Vector3(0.05f, h * 0.5f, 0.05f);
            tru.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.95f, 0.78f, 0.08f), 0.35f);

            // Bát sen tắm khẩn cấp
            var sen = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sen.name = "BatSenTam";
            sen.transform.SetParent(tram.transform, false);
            sen.transform.localPosition = new Vector3(0.25f, h - 0.05f, 0f);
            sen.transform.localScale = new Vector3(0.26f, 0.03f, 0.26f);
            sen.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.95f, 0.78f, 0.08f), 0.35f);

            // Bồn rửa mắt inox
            var bon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bon.name = "BonRuaMatInox";
            bon.transform.SetParent(tram.transform, false);
            bon.transform.localPosition = new Vector3(0.22f, 1.05f, 0f);
            bon.transform.localScale = new Vector3(0.32f, 0.06f, 0.32f);
            bon.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.92f, 0.92f, 0.95f));

            var col = tram.AddComponent<BoxCollider>();
            col.center = new Vector3(0.12f, h * 0.5f, 0f);
            col.size = new Vector3(0.6f, h, 0.6f);

            return tram;
        }

        public static GameObject TaoBinhChuaChayProcedural()
        {
            var binh = new GameObject("BinhChuaChay_CO2");
            float r = 0.075f, h = 0.50f;

            // Thân bình thép đỏ rực PBR
            var than = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            than.name = "ThanBinhDo";
            than.transform.SetParent(binh.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(r * 2f, h * 0.42f, r * 2f);
            than.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.88f, 0.12f, 0.12f), 0.55f);

            // Cụm van cò bóp
            var van = GameObject.CreatePrimitive(PrimitiveType.Cube);
            van.name = "CumVanCoBop";
            van.transform.SetParent(binh.transform, false);
            van.transform.localPosition = new Vector3(0f, h - 0.04f, 0f);
            van.transform.localScale = new Vector3(0.06f, 0.08f, 0.04f);
            van.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.2f, 0.2f, 0.22f));

            // Loa phun
            var loa = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            loa.name = "LoaPhun";
            loa.transform.SetParent(binh.transform, false);
            loa.transform.localPosition = new Vector3(0.08f, h * 0.55f, 0f);
            loa.transform.localScale = new Vector3(0.035f, 0.12f, 0.035f);
            loa.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.12f, 0.12f, 0.12f), 0.1f);

            var col = binh.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(r * 2.5f, h, r * 2.5f);

            return binh;
        }

        public static GameObject TaoDenBunsenProcedural()
        {
            var den = new GameObject("DenBunsen_KimLoai");
            float h = 0.15f;

            // Chân đế tròn gang nặng
            var chan = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chan.name = "ChanDeGang";
            chan.transform.SetParent(den.transform, false);
            chan.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            chan.transform.localScale = new Vector3(0.10f, 0.008f, 0.10f);
            chan.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.25f, 0.26f, 0.28f));

            // Ống đốt inox
            var ong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ong.name = "OngDotInox";
            ong.transform.SetParent(den.transform, false);
            ong.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            ong.transform.localScale = new Vector3(0.016f, h * 0.45f, 0.016f);
            ong.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.90f, 0.90f, 0.92f));

            // Van kim đồng thau
            var van = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            van.name = "VanKimDong";
            van.transform.SetParent(den.transform, false);
            van.transform.localPosition = new Vector3(0f, 0.032f, 0f);
            van.transform.localScale = new Vector3(0.022f, 0.012f, 0.022f);
            van.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.85f, 0.65f, 0.22f));

            var col = den.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(0.12f, h, 0.12f);

            return den;
        }

        public static GameObject TaoNgonLuaProcedural()
        {
            var lua = new GameObject("NgonLua_Bunsen");
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.name = "HinhLua";
            mesh.transform.SetParent(lua.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            mesh.transform.localScale = new Vector3(0.025f, 0.07f, 0.025f);
            mesh.GetComponent<Renderer>().material = LabVatLieu.PhatSang(new Color(0.2f, 0.7f, 1.0f));

            var light = lua.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 0.8f;
            light.intensity = 1.2f;
            light.color = new Color(0.3f, 0.7f, 1.0f);

            return lua;
        }

        public static GameObject TaoDongHoVanNangProcedural()
        {
            var dh = new GameObject("DongHoVanNang_MultiMeter");
            float w = 0.14f, h = 0.045f, d = 0.20f;

            // Thân máy vàng/cam bọc cao su chống sốc
            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "VoMayCaoSu";
            than.transform.SetParent(dh.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(w, h, d);
            than.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.96f, 0.55f, 0.10f), 0.2f);

            // Màn hình LCD kính đen
            var lcd = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lcd.name = "ManHinhLCD";
            lcd.transform.SetParent(dh.transform, false);
            lcd.transform.localPosition = new Vector3(0f, h + 0.001f, d * 0.24f);
            lcd.transform.localScale = new Vector3(w * 0.78f, 0.004f, d * 0.28f);
            lcd.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.08f, 0.12f, 0.14f), 0.6f);

            // Núm xoay thang đo tròn đen
            var num = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            num.name = "NumXoay";
            num.transform.SetParent(dh.transform, false);
            num.transform.localPosition = new Vector3(0f, h + 0.005f, -d * 0.12f);
            num.transform.localScale = new Vector3(0.045f, 0.01f, 0.045f);
            num.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.15f, 0.16f, 0.18f), 0.2f);

            var col = dh.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h + 0.01f, d);

            return dh;
        }

        public static GameObject TaoNguonDCProcedural()
        {
            var nguon = new GameObject("NguonDienDC_PowerSupply");
            float w = 0.22f, h = 0.15f, d = 0.20f;

            // Thân vỏ kim loại công nghiệp xám
            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "VoKimLoai";
            than.transform.SetParent(nguon.transform, false);
            than.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            than.transform.localScale = new Vector3(w, h, d);
            than.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.42f, 0.45f, 0.48f));

            // Bảng mặt trước chứa màn hình LED
            var matTruoc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matTruoc.name = "BangDieuKhien";
            matTruoc.transform.SetParent(nguon.transform, false);
            matTruoc.transform.localPosition = new Vector3(0f, h * 0.5f, d * 0.505f);
            matTruoc.transform.localScale = new Vector3(w * 0.92f, h * 0.88f, 0.008f);
            matTruoc.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.18f, 0.20f, 0.22f), 0.3f);

            var col = nguon.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);

            return nguon;
        }

        #endregion
    }
}
