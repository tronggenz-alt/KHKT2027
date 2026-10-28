using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.ToaNha
{
    [Serializable]
    public class ToaNhaThongSo
    {
        public string ten;
        public float rong;
        public float sau;
        public float cao_sanh;
        public float cao_phong;
        public float cao_hanh_lang;
        public float[] xuat_phat;
    }

    [Serializable]
    public class GiengTroiThongSo
    {
        public float[] tam;
        public float[] kich_thuoc;
        public float cao;
    }

    [Serializable]
    public class BangTuanHoanThongSo
    {
        public float[] vi_tri;
        public float[] goc_xoay;
        public float[] kich_thuoc;
    }

    [Serializable]
    public class CuaThongSo
    {
        public float[] tam;
        public float rong;
        public float cao;
        public string truc; // "x" hoac "z"
    }

    [Serializable]
    public class PhongThongSo
    {
        public string id;
        public string ten;
        public float[] hcn; // [x1, z1, x2, z2]
        public float cao;
        public string mau;
        public CuaThongSo cua;
    }

    [Serializable]
    public class HanhLangThongSo
    {
        public float[] hcn;
        public string ten;
    }

    [Serializable]
    public class TramThongSoToaNha
    {
        public string id;
        public string ten;
        public string phong;
        public float[] tam;
        public float[] kich_thuoc_ban;
        public float[] diem_dung;
        public float[] goc_nhin;
    }

    [Serializable]
    public class ToaNhaData
    {
        public ToaNhaThongSo toa_nha;
        public GiengTroiThongSo gieng_troi;
        public BangTuanHoanThongSo bang_tuan_hoan;
        public List<PhongThongSo> phong;
        public List<HanhLangThongSo> hanh_lang;
        public List<TramThongSoToaNha> tram;
    }

    /// <summary>
    /// Module dựng toà nhà phòng lab hiện đại 36m × 30m bằng code procedural:
    /// - 7 phòng chức năng nối nhau bằng hành lang rộng 3m
    /// - Sảnh lớn 12×10m trần cao 8m có giếng trời kính đón sáng tự nhiên
    /// - Bảng tuần hoàn 3D 7m × 3.6m ở tường sảnh
    /// - Vách kính cường lực và Cửa trượt tự động mở khi nhân vật lại gần (< 2.2m)
    /// - Reflection Probes ở sảnh và từng phòng cho phản xạ PBR chuẩn xác
    /// - NavMeshSurface phủ toàn bộ sàn toà nhà 36 × 30 m
    /// </summary>
    public class DungToaNha : MonoBehaviour
    {
        public static DungToaNha Instance { get; private set; }

        public string ToaNhaJsonPath = "DuLieu/toa-nha.json";
        public ToaNhaData DuLieu;
        public Transform GocToaNha;
        public GameObject TranNhaTong;
        public readonly Dictionary<string, Transform> CacBanTram = new Dictionary<string, Transform>();
        public readonly Dictionary<string, PhongThongSo> CacPhong = new Dictionary<string, PhongThongSo>();

        private NavMeshSurface _navSurface;

        void Awake()
        {
            Instance = this;
            if (DuLieu == null)
            {
                Dung();
            }
        }

        public void BatTatTranNha(bool bat)
        {
            if (TranNhaTong != null)
            {
                TranNhaTong.SetActive(bat);
            }
        }

        public void Dung()
        {
            string json = DocTaiNguyen.DocChuoi(ToaNhaJsonPath);
            if (string.IsNullOrEmpty(json))
            {
                json = DocTaiNguyen.DocChuoi("ptn/toa-nha.json");
            }
            if (string.IsNullOrEmpty(json))
            {
                json = DocTaiNguyen.DocChuoi("toa-nha.json");
            }

            if (!string.IsNullOrEmpty(json))
            {
                DuLieu = JsonUtility.FromJson<ToaNhaData>(json);
            }
            else
            {
                Debug.LogError("[DungToaNha] Không tìm thấy dữ liệu tòa nhà!");
                return;
            }

            if (GocToaNha != null) DestroyImmediate(GocToaNha.gameObject);
            GocToaNha = new GameObject("ToaNhaLab36x30").transform;
            GocToaNha.SetParent(transform, false);

            CacBanTram.Clear();
            CacPhong.Clear();
            if (DuLieu.phong != null)
            {
                foreach (var p in DuLieu.phong) CacPhong[p.id] = p;
            }

            DungSanToaNha();
            DungTuongBaoNgoai();
            DungVachKinhVaCuaTruot();
            DungTranNhaVaGiengTroi();
            DungBangTuanHoanSanh();
            Dung5BanThiNghiem();
            DungNoiThat7Phong();
            DungAnhSangVaReflectionProbes();
            BakeNavMesh();
        }

        private void DungSanToaNha()
        {
            float rong = DuLieu.toa_nha.rong;
            float sau = DuLieu.toa_nha.sau;

            // Sàn vinyl xám nhạt cao cấp toàn toà nhà
            var san = GameObject.CreatePrimitive(PrimitiveType.Cube);
            san.name = "SanVinylToaNha";
            san.transform.SetParent(GocToaNha, false);
            san.transform.localPosition = new Vector3(rong * 0.5f, -0.1f, sau * 0.5f);
            san.transform.localScale = new Vector3(rong, 0.2f, sau);
            san.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.88f, 0.90f, 0.92f), 0.25f);

            // Viền phân chia phòng 10cm thanh lịch
            if (DuLieu.phong != null)
            {
                var vlVien = LabVatLieu.Dac(new Color(0.45f, 0.50f, 0.55f, 0.6f), 0.35f);
                foreach (var p in DuLieu.phong)
                {
                    if (p.hcn == null || p.hcn.Length < 4) continue;
                    float x1 = p.hcn[0], z1 = p.hcn[1], x2 = p.hcn[2], z2 = p.hcn[3];
                    float kw = x2 - x1, kd = z2 - z1;

                    TaoVien(GocToaNha, $"Vien_{p.id}_B", new Vector3(x1 + kw * 0.5f, 0.005f, z1 + 0.05f), new Vector3(kw, 0.01f, 0.10f), vlVien);
                    TaoVien(GocToaNha, $"Vien_{p.id}_N", new Vector3(x1 + kw * 0.5f, 0.005f, z2 - 0.05f), new Vector3(kw, 0.01f, 0.10f), vlVien);
                    TaoVien(GocToaNha, $"Vien_{p.id}_T", new Vector3(x1 + 0.05f, 0.005f, z1 + kd * 0.5f), new Vector3(0.10f, 0.01f, kd), vlVien);
                    TaoVien(GocToaNha, $"Vien_{p.id}_D", new Vector3(x2 - 0.05f, 0.005f, z1 + kd * 0.5f), new Vector3(0.10f, 0.01f, kd), vlVien);
                }
            }
        }

        private void TaoVien(Transform parent, string ten, Vector3 pos, Vector3 size, Material mat)
        {
            var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = ten;
            v.transform.SetParent(parent, false);
            v.transform.localPosition = pos;
            v.transform.localScale = size;
            DestroyImmediate(v.GetComponent<Collider>());
            v.GetComponent<Renderer>().material = mat;
        }

        private void DungTuongBaoNgoai()
        {
            float rong = DuLieu.toa_nha.rong;
            float sau = DuLieu.toa_nha.sau;
            float caoP = DuLieu.toa_nha.cao_phong;
            float caoS = DuLieu.toa_nha.cao_sanh;

            var vlTuong = LabVatLieu.Dac(new Color(0.95f, 0.95f, 0.96f), 0.15f);

            // 1. Tường Bắc (z=0, dài 36m) có dải cửa sổ kính lớn lấy sáng tự nhiên
            TaoKhoiTuong("TuongBac_Duoi", new Vector3(rong * 0.5f, 0.6f, 0f), new Vector3(rong, 1.2f, 0.35f), vlTuong);
            TaoKhoiTuong("TuongBac_Tren", new Vector3(rong * 0.5f, caoP - 0.4f, 0f), new Vector3(rong, 0.8f, 0.35f), vlTuong);

            var kinhBac = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kinhBac.name = "KinhCuaSoBac_LaySang";
            kinhBac.transform.SetParent(GocToaNha, false);
            kinhBac.transform.localPosition = new Vector3(rong * 0.5f, 2.7f, 0f);
            kinhBac.transform.localScale = new Vector3(rong - 2f, 3.0f, 0.08f);
            kinhBac.GetComponent<Renderer>().material = LabVatLieu.ThuyTinh();

            // 2. Tường Nam (z=30, dài 36m): có cửa chính đại sảnh tại x=18m, rộng 3.2m
            float xCua = 18f;
            float rongCua = 3.2f;
            float wTrai = xCua - rongCua * 0.5f;
            float wPhai = rong - (xCua + rongCua * 0.5f);

            TaoKhoiTuong("TuongNam_Trai", new Vector3(wTrai * 0.5f, caoP * 0.5f, sau), new Vector3(wTrai, caoP, 0.35f), vlTuong);
            TaoKhoiTuong("TuongNam_Phai", new Vector3(rong - wPhai * 0.5f, caoP * 0.5f, sau), new Vector3(wPhai, caoP, 0.35f), vlTuong);
            TaoKhoiTuong("TuongNam_LanhTo", new Vector3(xCua, 5.75f, sau), new Vector3(rongCua, 4.5f, 0.35f), vlTuong); // Lanh tô lên tới trần sảnh 8m

            // 3. Tường Tây (x=0, sâu 30m) & Tường Đông (x=36, sâu 30m)
            TaoKhoiTuong("TuongTay", new Vector3(0f, caoP * 0.5f, sau * 0.5f), new Vector3(0.35f, caoP, sau), vlTuong);
            TaoKhoiTuong("TuongDong", new Vector3(rong, caoP * 0.5f, sau * 0.5f), new Vector3(0.35f, caoP, sau), vlTuong);
        }

        private void TaoKhoiTuong(string ten, Vector3 viTri, Vector3 kichThuoc, Material vl)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = ten;
            t.transform.SetParent(GocToaNha, false);
            t.transform.localPosition = viTri;
            t.transform.localScale = kichThuoc;
            t.GetComponent<Renderer>().material = vl;

            // Nẹp chân tường xám đậm (12cm)
            var len = GameObject.CreatePrimitive(PrimitiveType.Cube);
            len.name = ten + "_Len";
            len.transform.SetParent(GocToaNha, false);
            len.transform.localPosition = new Vector3(viTri.x, 0.06f, viTri.z);
            len.transform.localScale = new Vector3(kichThuoc.x * 1.01f, 0.12f, kichThuoc.z * 1.01f);
            DestroyImmediate(len.GetComponent<Collider>());
            len.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.25f, 0.28f, 0.32f), 0.3f);
        }

        private void DungVachKinhVaCuaTruot()
        {
            if (DuLieu.phong == null) return;

            var vlKinh = LabVatLieu.ThuyTinh();
            var vlKhung = LabVatLieu.Dac(new Color(0.2f, 0.22f, 0.25f), 0.6f);

            foreach (var p in DuLieu.phong)
            {
                if (p.id == "sanh") continue; // Sảnh không ngăn vách kín
                if (p.hcn == null || p.hcn.Length < 4) continue;

                float x1 = p.hcn[0], z1 = p.hcn[1], x2 = p.hcn[2], z2 = p.hcn[3];
                float w = x2 - x1, d = z2 - z1;

                // Tạo vách kính ngăn với hành lang kèm cửa trượt tự động
                if (p.cua != null)
                {
                    TaoCuaTruotVaVach(p, vlKinh, vlKhung);
                }
            }
        }

        private void TaoCuaTruotVaVach(PhongThongSo p, Material vlKinh, Material vlKhung)
        {
            var c = p.cua;
            Vector3 viTriCua = new Vector3(c.tam[0], c.cao * 0.5f, c.tam[1]);

            var goCuaTong = new GameObject($"CuaTruotTuDong_{p.id}");
            goCuaTong.transform.SetParent(GocToaNha, false);
            goCuaTong.transform.localPosition = viTriCua;

            bool docTheoX = (c.truc == "x"); // Cửa nằm trên mặt phẳng x=const

            if (docTheoX)
            {
                goCuaTong.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Khung trên cửa (Header frame)
            var khungTren = GameObject.CreatePrimitive(PrimitiveType.Cube);
            khungTren.name = "KhungTren";
            khungTren.transform.SetParent(goCuaTong.transform, false);
            khungTren.transform.localPosition = new Vector3(0f, c.cao * 0.5f - 0.05f, 0f);
            khungTren.transform.localScale = new Vector3(c.rong * 1.1f, 0.1f, 0.12f);
            khungTren.GetComponent<Renderer>().material = vlKhung;

            // Hai cánh cửa kính trượt
            float canhW = c.rong * 0.52f;
            float canhH = c.cao - 0.1f;

            var cTrai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cTrai.name = "CanhTrai";
            cTrai.transform.SetParent(goCuaTong.transform, false);
            cTrai.transform.localPosition = new Vector3(-canhW * 0.5f, 0f, 0f);
            cTrai.transform.localScale = new Vector3(canhW, canhH, 0.04f);
            cTrai.GetComponent<Renderer>().material = vlKinh;

            var cPhai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cPhai.name = "CanhPhai";
            cPhai.transform.SetParent(goCuaTong.transform, false);
            cPhai.transform.localPosition = new Vector3(canhW * 0.5f, 0f, 0f);
            cPhai.transform.localScale = new Vector3(canhW, canhH, 0.04f);
            cPhai.GetComponent<Renderer>().material = vlKinh;

            // Gắn controller cửa trượt tự động
            var ctrl = goCuaTong.AddComponent<CuaTruotTuDong>();
            ctrl.CanhTrai = cTrai.transform;
            ctrl.CanhPhai = cPhai.transform;
            ctrl.DoMoRong = c.rong * 0.48f;
            ctrl.KhoangCachKichHoat = 2.4f;

            // Biển tên phòng mica gắn đèn LED ở trên cửa
            var bien = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bien.name = $"BienTen_{p.id}";
            bien.transform.SetParent(goCuaTong.transform, false);
            bien.transform.localPosition = new Vector3(0f, c.cao * 0.5f + 0.25f, 0f);
            bien.transform.localScale = new Vector3(1.6f, 0.35f, 0.06f);
            bien.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.08f, 0.16f, 0.28f), 0.5f);
        }

        private void DungTranNhaVaGiengTroi()
        {
            TranNhaTong = new GameObject("HeThongTranNha");
            TranNhaTong.transform.SetParent(GocToaNha, false);

            float rong = DuLieu.toa_nha.rong;
            float sau = DuLieu.toa_nha.sau;
            float caoP = DuLieu.toa_nha.cao_phong;
            float caoS = DuLieu.toa_nha.cao_sanh;

            var vlTran = LabVatLieu.Dac(new Color(0.96f, 0.96f, 0.97f), 0.1f);

            // 1. Trần các phòng chức năng (cao 5.0m)
            var tranP = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tranP.name = "TranPhacThao_Phong5m";
            tranP.transform.SetParent(TranNhaTong.transform, false);
            tranP.transform.localPosition = new Vector3(rong * 0.5f, caoP + 0.1f, 10f); // Phía Bắc toà nhà
            tranP.transform.localScale = new Vector3(rong, 0.2f, 20f);
            tranP.GetComponent<Renderer>().material = vlTran;

            // 2. Trần sảnh đón tiếp (cao 8.0m) có lỗ giếng trời kính
            var tranS = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tranS.name = "TranSanh8m";
            tranS.transform.SetParent(TranNhaTong.transform, false);
            tranS.transform.localPosition = new Vector3(rong * 0.5f, caoS + 0.1f, 25f); // Phía Nam toà nhà
            tranS.transform.localScale = new Vector3(rong, 0.2f, 10f);
            tranS.GetComponent<Renderer>().material = vlTran;

            // 3. Giếng trời kính đón sáng tự nhiên (Skylight 8m × 6m tại sảnh)
            if (DuLieu.gieng_troi != null)
            {
                var gt = DuLieu.gieng_troi;
                var kinhGiengTroi = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kinhGiengTroi.name = "KinhGiengTroi_Skylight";
                kinhGiengTroi.transform.SetParent(GocToaNha, false);
                kinhGiengTroi.transform.localPosition = new Vector3(gt.tam[0], gt.cao + 0.15f, gt.tam[1]);
                kinhGiengTroi.transform.localScale = new Vector3(gt.kich_thuoc[0], 0.08f, gt.kich_thuoc[1]);
                kinhGiengTroi.GetComponent<Renderer>().material = LabVatLieu.ThuyTinh();

                // Khung nhôm giếng trời
                var khungGT = GameObject.CreatePrimitive(PrimitiveType.Cube);
                khungGT.name = "KhungGiengTroi";
                khungGT.transform.SetParent(GocToaNha, false);
                khungGT.transform.localPosition = new Vector3(gt.tam[0], gt.cao + 0.2f, gt.tam[1]);
                khungGT.transform.localScale = new Vector3(gt.kich_thuoc[0] + 0.3f, 0.12f, gt.kich_thuoc[1] + 0.3f);
                khungGT.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.18f, 0.2f, 0.22f), 0.7f);
            }
        }

        private void DungBangTuanHoanSanh()
        {
            // Bảng tuần hoàn 3D lớn 7m × 3.6m đặt trang trọng tại sảnh
            var bang = BangTuanHoan3D.TaoBangSanh(GocToaNha);
            bang.transform.localPosition = new Vector3(DuLieu.bang_tuan_hoan.vi_tri[0], DuLieu.bang_tuan_hoan.vi_tri[1], DuLieu.bang_tuan_hoan.vi_tri[2]);
            bang.transform.localRotation = Quaternion.Euler(DuLieu.bang_tuan_hoan.goc_xoay[0], DuLieu.bang_tuan_hoan.goc_xoay[1], DuLieu.bang_tuan_hoan.goc_xoay[2]);

            // Poster nhỏ tại các phòng
            BangTuanHoan3D.TaoPosterPhong(GocToaNha, new Vector3(6.5f, 2.0f, 1.2f), Vector3.zero);
            BangTuanHoan3D.TaoPosterPhong(GocToaNha, new Vector3(2.2f, 2.0f, 15.0f), new Vector3(0f, 90f, 0f));
            BangTuanHoan3D.TaoPosterPhong(GocToaNha, new Vector3(23.5f, 2.0f, 1.2f), Vector3.zero);
            BangTuanHoan3D.TaoPosterPhong(GocToaNha, new Vector3(31.5f, 2.0f, 1.2f), Vector3.zero);
        }

        private void Dung5BanThiNghiem()
        {
            CacBanTram.Clear();
            if (DuLieu.tram == null) return;

            var vlPhenolic = LabVatLieu.Dac(new Color(0.06f, 0.18f, 0.32f), 0.7f); // Mặt phenolic xanh Navy Hoà Phát
            var vlGo = LabVatLieu.Dac(new Color(0.85f, 0.72f, 0.56f), 0.3f);       // Tủ gỗ sồi sáng
            var vlInox = LabVatLieu.KimLoai(new Color(0.85f, 0.85f, 0.88f));

            foreach (var tr in DuLieu.tram)
            {
                var gBan = new GameObject("BanTram_" + tr.id);
                gBan.transform.SetParent(GocToaNha, false);

                float bw = tr.kich_thuoc_ban[0];
                float bh = tr.kich_thuoc_ban[1];
                float bd = tr.kich_thuoc_ban[2];
                gBan.transform.localPosition = new Vector3(tr.tam[0], 0f, tr.tam[1]);

                // Mặt bàn phenolic
                var matBan = GameObject.CreatePrimitive(PrimitiveType.Cube);
                matBan.name = "MatPhenolic";
                matBan.transform.SetParent(gBan.transform, false);
                matBan.transform.localPosition = new Vector3(0f, bh, 0f);
                matBan.transform.localScale = new Vector3(bw, 0.04f, bd);
                matBan.GetComponent<Renderer>().material = vlPhenolic;

                // Tủ dưới gỗ sồi
                var tu = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tu.name = "TuGoSoi";
                tu.transform.SetParent(gBan.transform, false);
                tu.transform.localPosition = new Vector3(0f, (bh - 0.04f) * 0.5f, 0f);
                tu.transform.localScale = new Vector3(bw * 0.92f, bh - 0.04f, bd * 0.85f);
                tu.GetComponent<Renderer>().material = vlGo;

                // Gốc đặt thí nghiệm (trên mặt bàn)
                var gocTN = new GameObject("GocThiNghiem_" + tr.id);
                gocTN.transform.SetParent(gBan.transform, false);
                gocTN.transform.localPosition = new Vector3(0f, bh + 0.02f, 0f);

                CacBanTram[tr.id] = gocTN.transform;
            }
        }

        private void DungNoiThat7Phong()
        {
            var vlInox = LabVatLieu.KimLoai(new Color(0.85f, 0.85f, 0.88f));
            var vlXam = LabVatLieu.Dac(new Color(0.75f, 0.77f, 0.8f), 0.3f);
            var vlDo = LabVatLieu.Dac(new Color(0.85f, 0.15f, 0.15f), 0.5f);

            // Tủ hút khí độc Lý Sơn Sa Kỳ tại phòng chuẩn độ & pha chế
            if (KhoMoHinh.Instance != null)
            {
                KhoMoHinh.Instance.TaoVatThe("tu_hut", GocToaNha, new Vector3(2.5f, 0f, 3.5f), Quaternion.identity);
                KhoMoHinh.Instance.TaoVatThe("tu_hut", GocToaNha, new Vector3(33.5f, 0f, 14.0f), Quaternion.Euler(0f, -90f, 0f));
                KhoMoHinh.Instance.TaoVatThe("an_toan", GocToaNha, new Vector3(2.6f, 0f, 22.5f), Quaternion.identity);
                KhoMoHinh.Instance.TaoVatThe("binh_chua_chay", GocToaNha, new Vector3(10.2f, 0f, 23.5f), Quaternion.identity);
                KhoMoHinh.Instance.TaoVatThe("ghe", GocToaNha, new Vector3(6.5f, 0f, 6.8f), Quaternion.identity);
                KhoMoHinh.Instance.TaoVatThe("ghe", GocToaNha, new Vector3(23.5f, 0f, 6.8f), Quaternion.identity);
            }
            else
            {
                TaoTuHutKhiDoc(new Vector3(2.5f, 0f, 3.5f), Vector3.zero);
                TaoTuHutKhiDoc(new Vector3(33.5f, 0f, 14.0f), new Vector3(0f, -90f, 0f));
                TaoTramRuaMat(new Vector3(2.6f, 0f, 22.5f));
            }

            // Bàn cân phân tích tại phòng cân & pha chế
            TaoBanCanPhanTich(new Vector3(30.0f, 0f, 15.5f));
        }

        private void TaoTuHutKhiDoc(Vector3 viTri, Vector3 gocXoay)
        {
            var g = new GameObject("TuHutKhiDoc_LySonSaKy");
            g.transform.SetParent(GocToaNha, false);
            g.transform.localPosition = viTri;
            g.transform.localRotation = Quaternion.Euler(gocXoay);

            var than = GameObject.CreatePrimitive(PrimitiveType.Cube);
            than.name = "ThanTu";
            than.transform.SetParent(g.transform, false);
            than.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            than.transform.localScale = new Vector3(1.5f, 2.4f, 0.9f);
            than.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.9f, 0.92f, 0.94f), 0.4f);

            var kinhTruot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kinhTruot.name = "KinhTruotDoiTrong";
            kinhTruot.transform.SetParent(g.transform, false);
            kinhTruot.transform.localPosition = new Vector3(0f, 1.25f, 0.42f);
            kinhTruot.transform.localScale = new Vector3(1.35f, 1.0f, 0.03f);
            kinhTruot.GetComponent<Renderer>().material = LabVatLieu.ThuyTinh();
        }

        private void TaoTramRuaMat(Vector3 viTri)
        {
            var g = new GameObject("TramRuaMatKhanCap");
            g.transform.SetParent(GocToaNha, false);
            g.transform.localPosition = viTri;

            var cot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cot.transform.SetParent(g.transform, false);
            cot.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            cot.transform.localScale = new Vector3(0.08f, 1.1f, 0.08f);
            cot.GetComponent<Renderer>().material = LabVatLieu.KimLoai(new Color(0.85f, 0.85f, 0.88f));

            var chau = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chau.transform.SetParent(g.transform, false);
            chau.transform.localPosition = new Vector3(0f, 1.05f, 0.2f);
            chau.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            chau.GetComponent<Renderer>().material = LabVatLieu.KimLoai(Color.white);
        }

        private void TaoBanCanPhanTich(Vector3 viTri)
        {
            var g = new GameObject("BanCanPhanTich");
            g.transform.SetParent(GocToaNha, false);
            g.transform.localPosition = viTri;

            var ban = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ban.transform.SetParent(g.transform, false);
            ban.transform.localPosition = new Vector3(0f, 0.425f, 0f);
            ban.transform.localScale = new Vector3(1.8f, 0.85f, 0.9f);
            ban.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.2f, 0.25f, 0.3f), 0.5f);
        }

        private void DungAnhSangVaReflectionProbes()
        {
            // 1. Ánh sáng tự nhiên từ giếng trời sảnh (Sunlight)
            var gSun = new GameObject("SunLight_GiengTroi");
            gSun.transform.SetParent(GocToaNha, false);
            gSun.transform.localPosition = new Vector3(18f, 12f, 25f);
            gSun.transform.localRotation = Quaternion.Euler(60f, -25f, 0f);
            var lSun = gSun.AddComponent<Light>();
            lSun.type = LightType.Directional;
            lSun.color = new Color(1f, 0.98f, 0.95f);
            lSun.intensity = 1.25f;
            lSun.shadows = LightShadows.Soft;

            // 2. Đèn chiếu sáng sảnh đón tiếp
            var gSanh = new GameObject("DenSanh_LED");
            gSanh.transform.SetParent(GocToaNha, false);
            gSanh.transform.localPosition = new Vector3(18f, 7.5f, 25f);
            var lSanh = gSanh.AddComponent<Light>();
            lSanh.type = LightType.Point;
            lSanh.range = 22f;
            lSanh.color = new Color(0.95f, 0.98f, 1.0f);
            lSanh.intensity = 1.3f;

            // 3. Đèn hành lang trung tâm
            var gHanhLang = new GameObject("DenHanhLang_LED");
            gHanhLang.transform.SetParent(GocToaNha, false);
            gHanhLang.transform.localPosition = new Vector3(18f, 3.8f, 14f);
            var lHL = gHanhLang.AddComponent<Light>();
            lHL.type = LightType.Point;
            lHL.range = 18f;
            lHL.color = new Color(0.98f, 0.98f, 1.0f);
            lHL.intensity = 1.1f;

            // 4. Reflection Probes cho sảnh và hành lang để kính/inox không bao giờ bị đen
            TaoReflectionProbe(GocToaNha, "Probe_Sanh", new Vector3(18f, 2.5f, 25f), new Vector3(16f, 9f, 12f));
            TaoReflectionProbe(GocToaNha, "Probe_HanhLang", new Vector3(18f, 2.2f, 14f), new Vector3(20f, 5f, 14f));
            TaoReflectionProbe(GocToaNha, "Probe_KhuTayBac", new Vector3(8f, 2.2f, 6f), new Vector3(16f, 6f, 12f));
            TaoReflectionProbe(GocToaNha, "Probe_KhuDongBac", new Vector3(28f, 2.2f, 6f), new Vector3(16f, 6f, 12f));
        }

        private void TaoReflectionProbe(Transform parent, string ten, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(ten);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.size = size;
            probe.intensity = 0.85f;
        }

        private void BakeNavMesh()
        {
            _navSurface = GocToaNha.gameObject.AddComponent<NavMeshSurface>();
            _navSurface.collectObjects = CollectObjects.All;
            _navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _navSurface.BuildNavMesh();
            Debug.Log("[DungToaNha] Đã bake NavMesh phủ toàn bộ toà nhà 36x30m và 7 phòng!");
        }

        public Transform LayBanTram(string tramId)
        {
            if (string.IsNullOrEmpty(tramId)) return null;
            if (CacBanTram.TryGetValue(tramId, out var t)) return t;
            if (tramId == "nhan-biet-ion" && CacBanTram.TryGetValue("nhan-biet", out var t1)) return t1;
            if (tramId == "nhan-biet" && CacBanTram.TryGetValue("nhan-biet-ion", out var t2)) return t2;
            return null;
        }
    }
}
