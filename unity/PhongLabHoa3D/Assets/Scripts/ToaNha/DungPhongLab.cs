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
    public class BanDoData
    {
        public PhongData phong;
        public XuatPhatData xuat_phat;
        public List<KhuData> khu;
        public List<PhongConData> phong_con;
        public List<TramData> tram;
        public List<NoiThatData> noi_that;
    }

    [Serializable] public class PhongData { public float rong; public float sau; public float cao; public float cao_ban; }
    [Serializable] public class XuatPhatData { public float x; public float z; public float huong; }
    [Serializable] public class KhuData { public string id; public string ten; public float[] hcn; }
    [Serializable] public class VachData { public float x1; public float z1; public float x2; public float z2; public float cao; public bool chan_camera; }
    [Serializable] public class PhongConData { public string khu; public string ten; public List<VachData> vach; }
    [Serializable] public class TramData { public string id; public string tn; public string khu; public float[] tam; public float huong; public float[] ban; public float[] diem_dung; public float ban_kinh_tuong_tac; }
    [Serializable] public class NoiThatData { public string id; public string loai; public float[] tam; public float[] kich_thuoc; public float huong; public bool chan; }

    public class DungPhongLab : MonoBehaviour
    {
        public static DungPhongLab Instance { get; private set; }

        public BanDoData DuLieu { get; private set; }
        public readonly Dictionary<string, Transform> CacBanTram = new Dictionary<string, Transform>();
        public Transform GocPhong { get; private set; }
        public GameObject TranNha { get; private set; }

        public void BatTatTranNha(bool bat)
        {
            if (TranNha != null) TranNha.SetActive(bat);
        }

        private NavMeshSurface _navSurface;

        void Awake()
        {
            Instance = this;
            DungToanBoPhong();
        }

        public void DungToanBoPhong(string banDoJsonPath = null)
        {
            Instance = this;
            string duongDan = string.IsNullOrEmpty(banDoJsonPath) ? "ptn/ban-do.json" : banDoJsonPath;
            string json = DocTaiNguyen.DocChuoi(duongDan);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[DungPhongLab] Không tìm thấy file bản đồ: {duongDan}");
                return;
            }

            DuLieu = JsonUtility.FromJson<BanDoData>(json);

            if (GocPhong != null) DestroyImmediate(GocPhong.gameObject);
            GocPhong = new GameObject("PhongLab22x16").transform;
            GocPhong.SetParent(transform, false);

            DungSanVaKhu();
            DungTuongNgoai();
            DungVachKinh();
            DungTranNha();
            Dung5BanThiNghiem();
            DungNoiThat();
            DungAnhSang();
            DungBangTuanHoan();
            BakeNavMesh();
        }

        private void DungSanVaKhu()
        {
            float rong = DuLieu.phong.rong;
            float sau = DuLieu.phong.sau;

            // Sàn chính: Sàn vinyl xám nhạt cao cấp chuẩn phòng thí nghiệm
            var san = GameObject.CreatePrimitive(PrimitiveType.Cube);
            san.name = "SanPhong";
            san.transform.SetParent(GocPhong, false);
            san.transform.localPosition = new Vector3(rong * 0.5f, -0.1f, sau * 0.5f);
            san.transform.localScale = new Vector3(rong, 0.2f, sau);
            san.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.88f, 0.90f, 0.92f), 0.25f);

            // Viền phân chia từng khu: viền mỏng 10cm, màu sắc trung tính nhẹ nhàng, không chói neon
            if (DuLieu.khu != null)
            {
                foreach (var k in DuLieu.khu)
                {
                    if (k.hcn == null || k.hcn.Length < 4) continue;
                    float x1 = k.hcn[0], z1 = k.hcn[1], x2 = k.hcn[2], z2 = k.hcn[3];
                    float kw = x2 - x1, kd = z2 - z1;

                    Color mauKhu = LayMauKhu(k.id);
                    Color mauVien = Color.Lerp(mauKhu, new Color(0.6f, 0.65f, 0.70f), 0.45f);
                    var vlVien = LabVatLieu.Dac(new Color(mauVien.r, mauVien.g, mauVien.b, 0.75f), 0.35f);

                    TaoVienKhu(GocPhong, $"Vien_{k.id}_B", new Vector3(x1 + kw * 0.5f, 0.005f, z1 + 0.05f), new Vector3(kw, 0.01f, 0.10f), vlVien);
                    TaoVienKhu(GocPhong, $"Vien_{k.id}_N", new Vector3(x1 + kw * 0.5f, 0.005f, z2 - 0.05f), new Vector3(kw, 0.01f, 0.10f), vlVien);
                    TaoVienKhu(GocPhong, $"Vien_{k.id}_T", new Vector3(x1 + 0.05f, 0.005f, z1 + kd * 0.5f), new Vector3(0.10f, 0.01f, kd), vlVien);
                    TaoVienKhu(GocPhong, $"Vien_{k.id}_D", new Vector3(x2 - 0.05f, 0.005f, z1 + kd * 0.5f), new Vector3(0.10f, 0.01f, kd), vlVien);
                }
            }
        }

        private void TaoVienKhu(Transform parent, string ten, Vector3 pos, Vector3 size, Material mat)
        {
            var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = ten;
            v.transform.SetParent(parent, false);
            v.transform.localPosition = pos;
            v.transform.localScale = size;
            DestroyImmediate(v.GetComponent<Collider>());
            v.GetComponent<Renderer>().material = mat;
        }

        private Color LayMauKhu(string id)
        {
            switch (id)
            {
                case "an-toan": return new Color(0.12f, 0.55f, 0.45f); // Emerald mờ
                case "dinh-luong": return new Color(0.15f, 0.45f, 0.70f); // Blue mờ
                case "dinh-tinh": return new Color(0.75f, 0.45f, 0.18f); // Amber mờ
                case "dien-hoa": return new Color(0.45f, 0.30f, 0.70f); // Purple mờ
                case "trung-tam": return new Color(0.25f, 0.40f, 0.55f); // Slate mờ
                default: return Color.gray;
            }
        }

        private void DungTuongNgoai()
        {
            float rong = DuLieu.phong.rong;
            float sau = DuLieu.phong.sau;
            float cao = DuLieu.phong.cao;
            var vlTuong = LabVatLieu.Dac(new Color(0.95f, 0.95f, 0.96f), 0.15f); // Trắng ngà

            // Tường Bắc (z = 0) có cửa sổ
            TaoTuong("TuongBac_Trai", new Vector3(0.75f, cao * 0.5f, 0f), new Vector3(1.5f, cao, 0.3f), vlTuong);
            TaoTuong("TuongBac_Duoi", new Vector3(11f, 0.5f, 0f), new Vector3(19f, 1.0f, 0.3f), vlTuong);
            TaoTuong("TuongBac_Tren", new Vector3(11f, 3.9f, 0f), new Vector3(19f, 0.6f, 0.3f), vlTuong);
            TaoTuong("TuongBac_Phai", new Vector3(21.25f, cao * 0.5f, 0f), new Vector3(1.5f, cao, 0.3f), vlTuong);

            // Kính cửa sổ Bắc
            var kinhCuaSo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kinhCuaSo.name = "KinhCuaSoBac";
            kinhCuaSo.transform.SetParent(GocPhong, false);
            kinhCuaSo.transform.localPosition = new Vector3(11f, 2.3f, 0f);
            kinhCuaSo.transform.localScale = new Vector3(19f, 2.6f, 0.05f);
            kinhCuaSo.GetComponent<Renderer>().material = LabVatLieu.ThuyTinh();

            // Tường Nam (z = sau, có cửa chính x=11, rộng 2.4m)
            float n1W = (rong - 2.4f) * 0.5f;
            TaoTuong("TuongNam_Trai", new Vector3(n1W * 0.5f, cao * 0.5f, sau), new Vector3(n1W, cao, 0.3f), vlTuong);
            TaoTuong("TuongNam_Phai", new Vector3(rong - n1W * 0.5f, cao * 0.5f, sau), new Vector3(n1W, cao, 0.3f), vlTuong);
            TaoTuong("TuongNam_LanhTo", new Vector3(rong * 0.5f, 3.6f, sau), new Vector3(2.4f, 1.2f, 0.3f), vlTuong);

            // Tường Tây (x = 0) & Đông (x = rong)
            TaoTuong("TuongTay", new Vector3(0f, cao * 0.5f, sau * 0.5f), new Vector3(0.3f, cao, sau), vlTuong);
            TaoTuong("TuongDong", new Vector3(rong, cao * 0.5f, sau * 0.5f), new Vector3(0.3f, cao, sau), vlTuong);
        }

        private void TaoTuong(string ten, Vector3 viTri, Vector3 kichThuoc, Material vl)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = ten;
            t.transform.SetParent(GocPhong, false);
            t.transform.localPosition = viTri;
            t.transform.localScale = kichThuoc;
            t.GetComponent<Renderer>().material = vl;

            // Nẹp chân tường xám đậm (12cm)
            var len = GameObject.CreatePrimitive(PrimitiveType.Cube);
            len.name = ten + "_LenChanTuong";
            len.transform.SetParent(GocPhong, false);
            len.transform.localPosition = new Vector3(viTri.x, 0.06f, viTri.z);
            len.transform.localScale = new Vector3(kichThuoc.x * 1.01f, 0.12f, kichThuoc.z * 1.01f);
            DestroyImmediate(len.GetComponent<Collider>());
            len.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.25f, 0.28f, 0.32f), 0.3f);
        }

        private void DungVachKinh()
        {
            if (DuLieu.phong_con == null) return;
            var vlKinh = LabVatLieu.ThuyTinh();
            var vlKhung = LabVatLieu.Dac(new Color(0.2f, 0.22f, 0.25f), 0.5f);

            foreach (var pc in DuLieu.phong_con)
            {
                if (pc.vach == null) continue;
                foreach (var v in pc.vach)
                {
                    float dx = v.x2 - v.x1;
                    float dz = v.z2 - v.z1;
                    float dai = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dai < 0.05f) continue;

                    Vector3 mid = new Vector3((v.x1 + v.x2) * 0.5f, v.cao * 0.5f, (v.z1 + v.z2) * 0.5f);
                    float gocY = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;

                    var gVach = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gVach.name = $"Vach_{pc.khu}_{dai:F1}m";
                    gVach.transform.SetParent(GocPhong, false);
                    gVach.transform.localPosition = mid;
                    gVach.transform.localRotation = Quaternion.Euler(0f, gocY, 0f);
                    gVach.transform.localScale = new Vector3(0.08f, v.cao, dai);
                    gVach.GetComponent<Renderer>().material = vlKinh;

                    // Khung nhôm chân vách
                    var chan = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chan.name = "ChanKhung";
                    chan.transform.SetParent(gVach.transform, false);
                    chan.transform.localPosition = new Vector3(0f, -0.45f, 0f);
                    chan.transform.localScale = new Vector3(1.3f, 0.08f, 1.0f);
                    chan.GetComponent<Renderer>().material = vlKhung;
                }
            }
        }

        private void DungTranNha()
        {
            float rong = DuLieu.phong.rong;
            float sau = DuLieu.phong.sau;
            float cao = DuLieu.phong.cao;

            TranNha = GameObject.CreatePrimitive(PrimitiveType.Cube);
            TranNha.name = "TranNha";
            TranNha.transform.SetParent(GocPhong, false);
            TranNha.transform.localPosition = new Vector3(rong * 0.5f, cao + 0.1f, sau * 0.5f);
            TranNha.transform.localScale = new Vector3(rong, 0.2f, sau);
            TranNha.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.96f, 0.96f, 0.97f), 0.1f);
        }

        private void Dung5BanThiNghiem()
        {
            CacBanTram.Clear();
            if (DuLieu.tram == null) return;

            var vlPhenolic = LabVatLieu.Dac(new Color(0.06f, 0.18f, 0.32f), 0.7f); // Mặt phenolic xanh Navy Hoà Phát
            var vlGo = LabVatLieu.Dac(new Color(0.85f, 0.72f, 0.56f), 0.3f);       // Tủ gỗ sồi sáng
            var vlInox = LabVatLieu.KimLoai(new Color(0.88f, 0.88f, 0.9f));        // Khung thép & bồn inox

            foreach (var tr in DuLieu.tram)
            {
                float bx = tr.tam[0];
                float bz = tr.tam[1];
                float bw = tr.ban[0];
                float bd = tr.ban[1];
                float h = DuLieu.phong.cao_ban;

                var gBan = new GameObject("Ban_" + tr.id);
                gBan.transform.SetParent(GocPhong, false);
                gBan.transform.localPosition = new Vector3(bx, 0f, bz);
                gBan.transform.localRotation = Quaternion.Euler(0f, tr.huong * Mathf.Rad2Deg, 0f);

                // 1. Mặt bàn phenolic
                var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat.name = "MatBanPhenolic";
                mat.transform.SetParent(gBan.transform, false);
                mat.transform.localPosition = new Vector3(0f, h - 0.02f, 0f);
                mat.transform.localScale = new Vector3(bw, 0.04f, bd);
                mat.GetComponent<Renderer>().material = vlPhenolic;

                // Gờ chắn nước 80mm ở mép sau
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "GoChanNuoc";
                go.transform.SetParent(gBan.transform, false);
                go.transform.localPosition = new Vector3(0f, h + 0.04f, -bd * 0.5f + 0.02f);
                go.transform.localScale = new Vector3(bw, 0.08f, 0.04f);
                go.GetComponent<Renderer>().material = vlPhenolic;

                // 2. Hộc tủ bên dưới
                var tu = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tu.name = "HocTuDuoi";
                tu.transform.SetParent(gBan.transform, false);
                tu.transform.localPosition = new Vector3(0f, (h - 0.04f) * 0.5f, 0f);
                tu.transform.localScale = new Vector3(bw - 0.15f, h - 0.06f, bd - 0.15f);
                tu.GetComponent<Renderer>().material = vlGo;

                // 3. Khung chân thép
                var chan1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chan1.name = "ChanThepTrai";
                chan1.transform.SetParent(gBan.transform, false);
                chan1.transform.localPosition = new Vector3(-bw * 0.5f + 0.03f, h * 0.5f, 0f);
                chan1.transform.localScale = new Vector3(0.06f, h, bd);
                chan1.GetComponent<Renderer>().material = vlInox;

                var chan2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chan2.name = "ChanThepPhai";
                chan2.transform.SetParent(gBan.transform, false);
                chan2.transform.localPosition = new Vector3(bw * 0.5f - 0.03f, h * 0.5f, 0f);
                chan2.transform.localScale = new Vector3(0.06f, h, bd);
                chan2.GetComponent<Renderer>().material = vlInox;

                // 4. Transform gốc đặt thí nghiệm (nằm trên mặt bàn)
                var gocThiNghiem = new GameObject("GocThiNghiem_" + tr.id);
                gocThiNghiem.transform.SetParent(gBan.transform, false);
                gocThiNghiem.transform.localPosition = new Vector3(0f, h, 0f);

                CacBanTram[tr.id] = gocThiNghiem.transform;
            }
        }

        private void DungNoiThat()
        {
            if (DuLieu.noi_that == null) return;
            var vlInox = LabVatLieu.KimLoai(new Color(0.85f, 0.85f, 0.88f));
            var vlXam = LabVatLieu.Dac(new Color(0.75f, 0.77f, 0.8f), 0.3f);
            var vlDo = LabVatLieu.Dac(new Color(0.85f, 0.15f, 0.15f), 0.5f);

            foreach (var nt in DuLieu.noi_that)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "NoiThat_" + nt.id;
                g.transform.SetParent(GocPhong, false);
                float h = nt.kich_thuoc[1];
                g.transform.localPosition = new Vector3(nt.tam[0], h * 0.5f, nt.tam[1]);
                g.transform.localScale = new Vector3(nt.kich_thuoc[0], h, nt.kich_thuoc[2]);

                if (nt.loai == "binh_chua_chay") g.GetComponent<Renderer>().material = vlDo;
                else if (nt.loai == "voi_khan_cap" || nt.loai == "bon_rua") g.GetComponent<Renderer>().material = vlInox;
                else g.GetComponent<Renderer>().material = vlXam;

                if (!nt.chan)
                {
                    DestroyImmediate(g.GetComponent<Collider>());
                }
            }
        }

        private void DungAnhSang()
        {
            var gSun = new GameObject("SunLight");
            gSun.transform.SetParent(GocPhong, false);
            gSun.transform.localPosition = new Vector3(11f, 8f, 8f);
            gSun.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var l = gSun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.98f, 0.94f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;

            // Đèn trần trung tâm
            var gPoint = new GameObject("DenTran");
            gPoint.transform.SetParent(GocPhong, false);
            gPoint.transform.localPosition = new Vector3(11f, 3.8f, 8f);
            var lp = gPoint.AddComponent<Light>();
            lp.type = LightType.Point;
            lp.range = 24f;
            lp.color = new Color(0.95f, 0.98f, 1f);
            lp.intensity = 1.2f;
        }

        private void DungBangTuanHoan()
        {
            // Bảng 3D lớn 7m × 3.6m tại tường sảnh đón tiếp
            BangTuanHoan3D.TaoBangSanh(GocPhong);

            // Poster nhỏ tại các phòng thí nghiệm nhánh
            BangTuanHoan3D.TaoPosterPhong(GocPhong, new Vector3(3.2f, 2.0f, 0.2f), Vector3.zero); // Phòng định lượng (chuẩn độ)
            BangTuanHoan3D.TaoPosterPhong(GocPhong, new Vector3(0.2f, 2.0f, 6.0f), new Vector3(0f, 90f, 0f)); // Phòng định tính (nhận biết)
            BangTuanHoan3D.TaoPosterPhong(GocPhong, new Vector3(21.8f, 2.0f, 6.0f), new Vector3(0f, -90f, 0f)); // Phòng điện hóa (pin & điện phân)
        }

        private void BakeNavMesh()
        {
            _navSurface = GocPhong.gameObject.AddComponent<NavMeshSurface>();
            _navSurface.collectObjects = CollectObjects.All;
            _navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _navSurface.BuildNavMesh();
            Debug.Log("[DungPhongLab] Đã bake NavMesh phủ sàn phòng 22x16m!");
        }

        public Transform LayBanTram(string tramId)
        {
            if (CacBanTram.TryGetValue(tramId, out var t)) return t;
            return null;
        }
    }
}
