// Chi tiết THỰC cho phòng thí nghiệm (Unity) – bản C# của frontend/hoa-hoc/phong-thi-nghiem/noi-that-thuc.js, thích ứng với toà nhà 36 × 30 m
// của DungToaNha. Tự treo vào luồng khởi động: đợi DungToaNha dựng xong rồi BỔ SUNG (không sửa DungToaNha / DungPhongLab):
//  • texture thật cho sàn / tường / trần / mặt bàn / tủ gỗ (VatLieuThuc – sinh bằng code, không ảnh ngoài)
//  • bàn thí nghiệm: khe cánh tủ + tay nắm + chân đế, gờ chắn nước, ổ cắm, van gas, hai giá lọ thuốc thử (chừa trống giữa để không che dụng cụ)
//  • tủ hút khí độc dựng đủ: tủ đế, mặt bàn, vách, mái hút, ống thoát lên trần, kính trượt có khung + tay kéo, đèn trong tủ, màn hình lưu lượng gió
//  • trạm rửa mắt khẩn cấp (chậu xanh, vòi, bàn đạp, vòi tắm, biển chữ thập), bàn cân phân tích (cân + buồng kính + màn hình)
//  • khung cửa sổ nhôm + bệ cửa dải kính Bắc, bình chữa cháy, đồng hồ, tấm đèn LED + đầu phun chữa cháy trên trần
// KHÔNG thêm collider (NavMesh đã bake) → chỉ thêm hình ảnh, nhân vật đi xuyên như trước. Gỡ file này = về bản cũ.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhongThiNghiem.ToaNha
{
    public static class ChiTietThuc
    {
        const string TEN_GOC = "ChiTietThuc";

        // ------------------------------------------------------------ treo vào luồng khởi động
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Treo()
        {
            var g = new GameObject("ChiTietThuc_Treo") { hideFlags = HideFlags.HideInHierarchy };
            g.AddComponent<TreoChiTiet>();
        }

        class TreoChiTiet : MonoBehaviour
        {
            void Update()
            {
                var d = DungToaNha.Instance;
                if (d == null || d.GocToaNha == null || d.DuLieu == null) return;
                Dung(d);
                Destroy(gameObject);
            }
        }

        // ------------------------------------------------------------ vật liệu dùng chung
        class VL
        {
            public Material San, Tuong, Tran, TranSanh, GachTuong, Epoxy, Go, Khe, Nhom, Trang, Den, Vang, Xanh, Do, LongChau, XanhLa, TrangLam, Kinh;
            public Material[] Lo;
            public Material[] Nap;
            public Material Nhan;
        }

        static VL _vl;

        static VL TaoVL(float rong, float sau)
        {
            var v = new VL();
            // vật liệu PBR tạo ngay (xám sáng tạm); texture màu + pháp tuyến + độ bóng tính ở luồng nền rồi gắn vào (xem Dung)
            v.San = VatLieuThuc.VatPBR("san", rong / 1.8f, sau / 1.8f, 1f);
            v.Tuong = VatLieuThuc.VatPBR("tuong", 3f, 1f, 0.5f);
            v.Tran = VatLieuThuc.VatPBR("tran", rong / 1.8f, 20f / 1.8f, 0.8f);
            v.TranSanh = VatLieuThuc.VatPBR("tran_sanh", rong / 1.8f, 10f / 1.8f, 0.8f);
            v.GachTuong = VatLieuThuc.VatPBR("gach_tuong", 1f, 1f, 1f);
            v.Epoxy = VatLieuThuc.VatPBR("epoxy", 1f, 1f, 0.6f);
            v.Go = VatLieuThuc.VatPBR("go", 3f, 1f, 0.8f);
            v.Khe = LabVatLieu.Dac(new Color(0.05f, 0.06f, 0.08f), 0.1f);
            v.Nhom = LabVatLieu.KimLoai(new Color(0.86f, 0.87f, 0.9f));
            v.Trang = LabVatLieu.Dac(new Color(0.94f, 0.95f, 0.97f), 0.45f);
            v.TrangLam = LabVatLieu.Dac(new Color(0.9f, 0.92f, 0.94f), 0.35f);
            v.Den = LabVatLieu.Dac(new Color(0.12f, 0.14f, 0.18f), 0.5f);
            v.Vang = LabVatLieu.Dac(new Color(0.96f, 0.7f, 0.05f), 0.5f);
            v.Xanh = LabVatLieu.Dac(new Color(0.15f, 0.39f, 0.92f), 0.5f);
            v.Do = LabVatLieu.Dac(new Color(0.86f, 0.15f, 0.15f), 0.5f);
            v.XanhLa = LabVatLieu.Dac(new Color(0.08f, 0.55f, 0.28f), 0.45f);
            v.LongChau = LabVatLieu.Dac(new Color(0.11f, 0.14f, 0.18f), 0.6f);
            v.Kinh = LabVatLieu.ThuyTinh();
            var mauLo = new[] { new Color(0.54f, 0.29f, 0.08f), new Color(0.48f, 0.25f, 0.06f), new Color(0.6f, 0.35f, 0.11f), new Color(0.35f, 0.63f, 0.47f), new Color(0.23f, 0.43f, 0.77f), new Color(0.72f, 0.77f, 0.82f) };
            v.Lo = new Material[mauLo.Length];
            for (int i = 0; i < mauLo.Length; i++) v.Lo[i] = LabVatLieu.Dac(mauLo[i], 0.8f);
            v.Nap = new[] { LabVatLieu.Dac(new Color(0.07f, 0.09f, 0.13f), 0.5f), LabVatLieu.Dac(new Color(0.97f, 0.98f, 0.99f), 0.5f), LabVatLieu.Dac(new Color(0.15f, 0.39f, 0.92f), 0.5f), LabVatLieu.Dac(new Color(0.86f, 0.15f, 0.15f), 0.5f) };
            v.Nhan = LabVatLieu.Dac(new Color(0.97f, 0.97f, 0.96f), 0.3f);
            return v;
        }

        // ------------------------------------------------------------ dựng hình cơ bản
        static GameObject Hop(Transform cha, string ten, Vector3 vt, Vector3 kt, Material m, bool dungBong = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = ten;
            g.transform.SetParent(cha, false);
            g.transform.localPosition = vt;
            g.transform.localScale = kt;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!dungBong) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static GameObject Tru(Transform cha, string ten, Vector3 vt, float ban, float cao, Material m, Vector3? xoay = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = ten;
            g.transform.SetParent(cha, false);
            g.transform.localPosition = vt;
            if (xoay.HasValue) g.transform.localRotation = Quaternion.Euler(xoay.Value);
            g.transform.localScale = new Vector3(ban * 2f, cao * 0.5f, ban * 2f);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static GameObject Luoi(Transform cha, string ten, Mesh mesh, Material m, Vector3 vt, Vector3? tiLe = null, float yaw = 0f, bool bong = false)
        {
            var g = new GameObject(ten);
            g.transform.SetParent(cha, false);
            g.transform.localPosition = vt;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (tiLe.HasValue) g.transform.localScale = tiLe.Value;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            if (mesh.subMeshCount == 2) r.sharedMaterials = LabVatLieu.HaiLuot(m); else r.sharedMaterial = m;
            if (!bong) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static float R(int i, int k)
        {
            float h = Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static TextMesh Chu(Transform cha, string chu, Vector3 vt, float co, Color mau, float yaw = 180f)
        {
            var font = LabFont.Get();
            var g = new GameObject("Chu");
            g.transform.SetParent(cha, false);
            g.transform.localPosition = vt;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);   // TextMesh đọc được từ phía -z; yaw 180 → đọc được từ phía +z
            g.transform.localScale = Vector3.one * co;
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = mau;
            tm.text = chu;
            g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        static Mesh _meshLo, _meshNhan;

        // ------------------------------------------------------------ vào chính
        public static void Dung(DungToaNha d)
        {
            if (d.GocToaNha.Find(TEN_GOC) != null) return;   // đã dựng
            var goc = new GameObject(TEN_GOC).transform;
            goc.SetParent(d.GocToaNha, false);
            float rong = d.DuLieu.toa_nha.rong, sau = d.DuLieu.toa_nha.sau;
            _vl = TaoVL(rong, sau);
            _meshLo = LabHinh.LatheMem(LabHinh.DungCu.LoThuoc(0.026f, 0.13f, 0.011f), 20);
            _meshNhan = LabHinh.LatheMem(new List<Vector3> { LabHinh.T(0.0268f, -0.025f), LabHinh.T(0.0268f, 0.025f) }, 20);

            // texture PBR: tính song song ở luồng nền, mỗi khung hình nhận về tối đa một bộ (không giật lúc mở app)
            VatLieuThuc.TaoNen(() => VatLieuThuc.SanGach(1024), t => VatLieuThuc.GanTex(_vl.San, t));
            VatLieuThuc.TaoNen(() => VatLieuThuc.GachOpTuong(1024), t => VatLieuThuc.GanTex(_vl.GachTuong, t));
            VatLieuThuc.TaoNen(() => VatLieuThuc.MatBanEpoxy(512), t => VatLieuThuc.GanTex(_vl.Epoxy, t));
            VatLieuThuc.TaoNen(() => VatLieuThuc.GoSoi(512), t => VatLieuThuc.GanTex(_vl.Go, t));
            VatLieuThuc.TaoNen(() => VatLieuThuc.TuongSon(256), t => VatLieuThuc.GanTex(_vl.Tuong, t));
            VatLieuThuc.TaoNen(() => VatLieuThuc.TranTam(512), t => { VatLieuThuc.GanTex(_vl.Tran, t); VatLieuThuc.GanTex(_vl.TranSanh, t); });
            goc.gameObject.AddComponent<BomTexture>();
            NangVatLieu(d);
            if (d.DuLieu.tram != null) foreach (var t in d.DuLieu.tram) Ban(d, goc, t);
            foreach (var t in d.GocToaNha.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "TuHutKhiDoc_LySonSaKy") TuHut(t);
                else if (t.name == "TramRuaMatKhanCap") RuaMat(t);
                else if (t.name == "BanCanPhanTich") BanCan(t);
            }
            Tran(d, goc);
            Tuong(d, goc);
            GachOpTuong(d, goc);
        }

        // ------------------------------------------------------------ 1. thay vật liệu phẳng bằng vật liệu có texture
        static void NangVatLieu(DungToaNha d)
        {
            foreach (var r in d.GocToaNha.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (n == "SanVinylToaNha") r.sharedMaterial = _vl.San;
                else if (n.StartsWith("Tuong") && !n.EndsWith("_Len")) r.sharedMaterial = _vl.Tuong;
                else if (n == "TranPhacThao_Phong5m") r.sharedMaterial = _vl.Tran;
                else if (n == "TranSanh8m") r.sharedMaterial = _vl.TranSanh;
                else if (n == "MatPhenolic") r.sharedMaterial = _vl.Epoxy;
                else if (n == "TuGoSoi") r.sharedMaterial = _vl.Go;
            }
        }

        // ------------------------------------------------------------ 2. bàn thí nghiệm
        static void Ban(DungToaNha d, Transform goc, TramThongSoToaNha tr)
        {
            var ban = d.GocToaNha.Find("BanTram_" + tr.id);
            if (ban == null) return;
            float bw = tr.kich_thuoc_ban[0], bh = tr.kich_thuoc_ban[1], bd = tr.kich_thuoc_ban[2];
            float yT = bh + 0.02f;                                // mặt trên mặt bàn
            float zf = bd * 0.85f * 0.5f + 0.001f;                // mặt trước thân tủ
            float W = bw * 0.92f, hTu = bh - 0.04f;
            var c = new GameObject("ChiTietBan").transform;
            c.SetParent(ban, false);

            // thân tủ: khe cánh + ngăn kéo + tay nắm + chân đế
            float yDay = hTu - 0.17f;                              // đáy ngăn kéo
            Hop(c, "KheGiua", new Vector3(0f, hTu * 0.5f, zf), new Vector3(0.004f, hTu - 0.06f, 0.002f), _vl.Khe);
            foreach (float dau in new[] { -1f, 1f })
            {
                float xm = dau * W * 0.25f;
                Hop(c, "KheCanh", new Vector3(xm, (0.1f + yDay) * 0.5f, zf), new Vector3(0.004f, yDay - 0.1f, 0.002f), _vl.Khe);
                Hop(c, "KheNganKeo", new Vector3(xm, yDay, zf), new Vector3(W * 0.49f, 0.004f, 0.002f), _vl.Khe);
                Hop(c, "TayNganKeo", new Vector3(xm, (yDay + hTu) * 0.5f, zf + 0.011f), new Vector3(0.14f, 0.012f, 0.016f), _vl.Nhom);
                foreach (float s in new[] { -1f, 1f })
                    Hop(c, "TayCanh", new Vector3(xm + s * 0.04f, hTu * 0.5f - 0.02f, zf + 0.011f), new Vector3(0.012f, 0.16f, 0.016f), _vl.Nhom);
            }
            Hop(c, "ChanDe", new Vector3(0f, 0.045f, zf + 0.0015f), new Vector3(W, 0.09f, 0.004f), _vl.Khe);

            // gờ chắn nước + ổ cắm + van gas
            float zSau = -bd * 0.5f;
            Hop(c, "GoChanNuoc", new Vector3(0f, yT + 0.04f, zSau + 0.015f), new Vector3(bw, 0.08f, 0.03f), _vl.Epoxy);
            foreach (float x in new[] { -0.55f, 0.45f })
            {
                Hop(c, "OCam", new Vector3(x, yT + 0.04f, zSau + 0.034f), new Vector3(0.085f, 0.058f, 0.008f), _vl.Trang);
                foreach (float dx in new[] { -0.02f, 0.02f })
                    Hop(c, "LoOCam", new Vector3(x + dx, yT + 0.045f, zSau + 0.0385f), new Vector3(0.016f, 0.022f, 0.003f), _vl.Khe);
            }
            foreach (float x in new[] { -0.25f, 0.1f })
            {
                Tru(c, "VanGas", new Vector3(x, yT + 0.03f, zSau + 0.07f), 0.009f, 0.06f, _vl.Nhom);
                Tru(c, "NumGas", new Vector3(x, yT + 0.068f, zSau + 0.07f), 0.017f, 0.016f, _vl.Vang);
            }

            // hai giá lọ thuốc thử, chừa trống giữa (|x| < 0,3 m) để không che dụng cụ thí nghiệm
            float zGia = zSau + 0.1f;
            foreach (float cao in new[] { 0.28f, 0.52f })
            {
                float yGia = yT + cao;
                Hop(c, "GiaThuoc", new Vector3(0f, yGia, zGia), new Vector3(1.4f, 0.02f, 0.16f), _vl.Go, true);
                float[] xs = { -0.7f, -0.6f, -0.5f, -0.4f, -0.3f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
                for (int i = 0; i < xs.Length; i++)
                {
                    float x = xs[i] + (R(i, 1) - 0.5f) * 0.012f, s = 0.82f + R(i + (int)(cao * 100f), 2) * 0.3f, yaw = R(i, 3) * 360f;
                    float yTop = yGia + 0.01f;
                    var mLo = _vl.Lo[Mathf.FloorToInt(R(i + (int)(cao * 100f), 5) * _vl.Lo.Length) % _vl.Lo.Length];
                    var mNap = _vl.Nap[Mathf.FloorToInt(R(i + (int)(cao * 100f), 6) * _vl.Nap.Length) % _vl.Nap.Length];
                    Luoi(c, "Lo", _meshLo, mLo, new Vector3(x, yTop, zGia), new Vector3(1f, s, 1f), yaw);
                    Luoi(c, "Nhan", _meshNhan, _vl.Nhan, new Vector3(x, yTop + 0.052f * s, zGia), new Vector3(1f, s, 1f), yaw);
                    Tru(c, "Nap", new Vector3(x, yTop + 0.13f * s + 0.007f, zGia), 0.0125f, 0.022f, mNap);
                }
            }
            foreach (float s in new[] { -1f, 1f })
                Tru(c, "TruGia", new Vector3(s * 0.68f, yT + 0.3f, zGia), 0.012f, 0.62f, _vl.Nhom);
        }

        // ------------------------------------------------------------ 3. tủ hút khí độc
        static void TuHut(Transform g)
        {
            var than = g.Find("ThanTu");
            if (than != null) than.GetComponent<Renderer>().enabled = false;   // giữ collider cũ, chỉ ẩn khối đặc
            var c = new GameObject("ChiTietTuHut").transform;
            c.SetParent(g, false);
            float zf = 0.451f;
            // tủ đế + khe cánh + tay nắm
            Hop(c, "TuDe", new Vector3(0f, 0.425f, 0f), new Vector3(1.5f, 0.85f, 0.9f), _vl.TrangLam, true);
            Hop(c, "KheGiua", new Vector3(0f, 0.43f, zf), new Vector3(0.004f, 0.78f, 0.002f), _vl.Khe);
            foreach (float s in new[] { -1f, 1f }) Hop(c, "TayCanh", new Vector3(s * 0.05f, 0.5f, zf + 0.011f), new Vector3(0.012f, 0.16f, 0.016f), _vl.Nhom);
            Hop(c, "ChanDe", new Vector3(0f, 0.04f, zf + 0.0015f), new Vector3(1.5f, 0.08f, 0.004f), _vl.Khe);
            // mặt bàn epoxy chịu hoá chất
            Hop(c, "MatBan", new Vector3(0f, 0.87f, 0f), new Vector3(1.52f, 0.04f, 0.92f), _vl.Epoxy, true);
            // vách bên, vách sau, mái hút
            foreach (float s in new[] { -1f, 1f }) Hop(c, "VachBen", new Vector3(s * 0.725f, 1.645f, 0f), new Vector3(0.05f, 1.51f, 0.9f), _vl.TrangLam, true);
            Hop(c, "VachSau", new Vector3(0f, 1.645f, -0.43f), new Vector3(1.4f, 1.51f, 0.04f), _vl.Trang, true);
            Hop(c, "MaiHut", new Vector3(0f, 2.275f, 0f), new Vector3(1.5f, 0.25f, 0.9f), _vl.TrangLam, true);
            Tru(c, "OngThoat", new Vector3(0f, 3.72f, -0.2f), 0.18f, 2.6f, _vl.Nhom);
            // đèn trong tủ + van gas / nước trên vách sau
            Hop(c, "DenTrongTu", new Vector3(0f, 2.14f, 0.12f), new Vector3(1.2f, 0.02f, 0.1f), VatLieuThuc.PhatSangDac(new Color(0.95f, 0.98f, 1f), 1.6f));
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                Tru(c, "VanTrongTu", new Vector3(x, 1.25f, -0.39f), 0.01f, 0.06f, _vl.Nhom, new Vector3(90f, 0f, 0f));
                Tru(c, "NumTrongTu", new Vector3(x, 1.25f, -0.35f), 0.016f, 0.016f, x < 0 ? _vl.Vang : _vl.Xanh, new Vector3(90f, 0f, 0f));
            }
            // kính trượt nâng hở 11 cm trên mặt bàn + khung nhôm + tay kéo
            var kinh = g.Find("KinhTruotDoiTrong");
            if (kinh != null) kinh.localPosition = new Vector3(0f, 1.52f, 0.42f);
            Hop(c, "KhungKinhTren", new Vector3(0f, 2.04f, 0.42f), new Vector3(1.42f, 0.04f, 0.045f), _vl.Nhom);
            Hop(c, "KhungKinhDuoi", new Vector3(0f, 1.0f, 0.42f), new Vector3(1.42f, 0.04f, 0.05f), _vl.Nhom);
            foreach (float s in new[] { -1f, 1f }) Hop(c, "KhungKinhBen", new Vector3(s * 0.7f, 1.52f, 0.42f), new Vector3(0.04f, 1.04f, 0.045f), _vl.Nhom);
            Hop(c, "TayKeoKinh", new Vector3(0f, 0.98f, 0.46f), new Vector3(0.4f, 0.022f, 0.03f), _vl.Nhom);
            // màn hình lưu lượng gió (xanh = an toàn)
            Hop(c, "ManHinhGio", new Vector3(0.52f, 2.2f, 0.46f), new Vector3(0.14f, 0.07f, 0.03f), _vl.Den);
            Hop(c, "DenGioXanh", new Vector3(0.52f, 2.2f, 0.4755f), new Vector3(0.05f, 0.02f, 0.002f), VatLieuThuc.PhatSangDac(new Color(0.2f, 0.95f, 0.45f), 1.8f));
        }

        // ------------------------------------------------------------ 4. trạm rửa mắt khẩn cấp
        static void RuaMat(Transform g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var c = new GameObject("ChiTietRuaMat").transform;
            c.SetParent(g, false);
            Tru(c, "DeSan", new Vector3(0f, 0.01f, 0f), 0.16f, 0.02f, _vl.Nhom);
            Tru(c, "OngDung", new Vector3(0f, 0.5f, 0f), 0.03f, 1.0f, _vl.Nhom);
            // chậu xanh có thành dày (giống thành thuỷ tinh): đặt trước ống
            var chau = new List<Vector3> { LabHinh.T(0f, 0f), LabHinh.B(0.07f, 0f, 0.02f), LabHinh.B(0.17f, 0.075f, 0.03f), LabHinh.T(0.185f, 0.09f) };
            var mesh = LabHinh.LatheMem(LabHinh.ThanhThuyTinh(chau, 0.005f, 0.006f), 40);
            Luoi(c, "ChauRuaMat", mesh, _vl.XanhLa, new Vector3(0f, 1.0f, 0.2f), null, 0f, true);
            Tru(c, "OngNoiChau", new Vector3(0f, 1.0f, 0.1f), 0.022f, 0.2f, _vl.Nhom, new Vector3(90f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                Tru(c, "VoiPhun", new Vector3(s * 0.07f, 1.075f, 0.1f), 0.012f, 0.07f, _vl.Nhom, new Vector3(0f, 0f, s * 25f));
                Tru(c, "DauPhun", new Vector3(s * 0.088f, 1.11f, 0.1f), 0.016f, 0.015f, _vl.Den, new Vector3(0f, 0f, s * 25f));
            }
            Hop(c, "BanDap", new Vector3(0f, 0.93f, 0.33f), new Vector3(0.1f, 0.012f, 0.14f), _vl.XanhLa);
            Tru(c, "CanBanDap", new Vector3(0f, 0.9f, 0.11f), 0.008f, 0.1f, _vl.Nhom, new Vector3(0f, 0f, 90f));
            // vòi tắm khẩn cấp phía trên + tay kéo vàng
            Tru(c, "OngTam", new Vector3(0f, 1.6f, 0f), 0.03f, 1.2f, _vl.Nhom);
            Tru(c, "TayVoiTam", new Vector3(0f, 2.22f, 0.12f), 0.025f, 0.26f, _vl.Nhom, new Vector3(90f, 0f, 0f));
            Tru(c, "DauTam", new Vector3(0f, 2.2f, 0.26f), 0.14f, 0.03f, _vl.Nhom);
            Tru(c, "DayKeo", new Vector3(0.06f, 1.95f, 0.26f), 0.005f, 0.5f, _vl.Nhom);
            Hop(c, "TayKeoVang", new Vector3(0.06f, 1.7f, 0.26f), new Vector3(0.14f, 0.02f, 0.02f), _vl.Vang);
            // biển chữ thập xanh
            Tru(c, "TayBien", new Vector3(0.15f, 1.85f, 0f), 0.008f, 0.3f, _vl.Nhom, new Vector3(0f, 0f, 90f));
            Hop(c, "BienXanh", new Vector3(0.33f, 1.85f, 0f), new Vector3(0.36f, 0.4f, 0.02f), _vl.XanhLa);
            Chu(c, "+", new Vector3(0.33f, 1.85f, 0.0115f), 0.025f, Color.white, 180f);   // mặt trước (phía +z): TextMesh yaw 180 đọc được từ +z
            Chu(c, "+", new Vector3(0.33f, 1.85f, -0.0115f), 0.025f, Color.white, 0f);    // mặt sau
        }

        // ------------------------------------------------------------ 5. bàn cân phân tích
        static void BanCan(Transform g)
        {
            var c = new GameObject("ChiTietBanCan").transform;
            c.SetParent(g, false);
            Hop(c, "MatBan", new Vector3(0f, 0.87f, 0f), new Vector3(1.84f, 0.04f, 0.94f), _vl.Epoxy, true);
            // mặt trước thân bàn (z = +0,45): khe cánh + ngăn kéo + tay nắm + chân đế, như bàn thí nghiệm
            float zf = 0.4515f;
            Hop(c, "KheGiua", new Vector3(0f, 0.42f, zf), new Vector3(0.004f, 0.7f, 0.002f), _vl.Khe);
            foreach (float dau in new[] { -1f, 1f })
            {
                float xm = dau * 0.45f;
                Hop(c, "KheNganKeo", new Vector3(xm, 0.64f, zf), new Vector3(0.88f, 0.004f, 0.002f), _vl.Khe);
                Hop(c, "TayNganKeo", new Vector3(xm, 0.735f, zf + 0.011f), new Vector3(0.14f, 0.012f, 0.016f), _vl.Nhom);
                Hop(c, "KheCanh", new Vector3(xm, 0.32f, zf), new Vector3(0.004f, 0.52f, 0.002f), _vl.Khe);
                foreach (float s in new[] { -1f, 1f }) Hop(c, "TayCanh", new Vector3(xm + s * 0.04f, 0.4f, zf + 0.011f), new Vector3(0.012f, 0.16f, 0.016f), _vl.Nhom);
            }
            Hop(c, "ChanDe", new Vector3(0f, 0.04f, zf + 0.0015f), new Vector3(1.8f, 0.08f, 0.004f), _vl.Khe);
            var vlXamCan = LabVatLieu.Dac(new Color(0.82f, 0.84f, 0.87f), 0.5f);
            Hop(c, "DeCan", new Vector3(0.3f, 0.92f, 0f), new Vector3(0.24f, 0.06f, 0.34f), vlXamCan, true);
            Hop(c, "ManHinhCan", new Vector3(0.3f, 0.935f, 0.178f), new Vector3(0.12f, 0.03f, 0.012f), VatLieuThuc.PhatSangDac(new Color(0.55f, 0.95f, 0.6f), 1.2f));
            Hop(c, "KhungBuongKinhDuoi", new Vector3(0.3f, 0.953f, -0.02f), new Vector3(0.22f, 0.008f, 0.26f), _vl.Nhom);
            Hop(c, "BuongKinh", new Vector3(0.3f, 1.06f, -0.02f), new Vector3(0.22f, 0.21f, 0.26f), _vl.Kinh);
            Hop(c, "KhungBuongKinhTren", new Vector3(0.3f, 1.168f, -0.02f), new Vector3(0.22f, 0.008f, 0.26f), _vl.Nhom);
            Tru(c, "DiaCan", new Vector3(0.3f, 0.98f, -0.02f), 0.05f, 0.008f, _vl.Nhom);
            // cốc mẫu bên cạnh + giấy cân
            var coc = LabHinh.Lathe(LabHinh.Coc(0.03f, 0.06f), 48);
            Luoi(c, "CocMau", coc, _vl.Kinh, new Vector3(-0.3f, 0.89f, 0.05f));
            Hop(c, "GiayCan", new Vector3(-0.05f, 0.8915f, 0.1f), new Vector3(0.09f, 0.002f, 0.09f), _vl.Trang);
        }

        // ------------------------------------------------------------ 6. trần: tấm đèn LED + đầu phun chữa cháy
        static void Tran(DungToaNha d, Transform goc)
        {
            float caoP = d.DuLieu.toa_nha.cao_phong, caoS = d.DuLieu.toa_nha.cao_sanh;
            var vlLed = VatLieuThuc.PhatSangDac(new Color(0.96f, 0.98f, 1f), 1.5f);
            var khung = LabVatLieu.KimLoai(new Color(0.8f, 0.82f, 0.85f));
            var vlDau = LabVatLieu.Dac(new Color(0.72f, 0.1f, 0.1f), 0.4f);
            foreach (var p in d.DuLieu.phong)
            {
                if (p.hcn == null || p.hcn.Length < 4 || p.id == "sanh") continue;
                float x1 = p.hcn[0], z1 = p.hcn[1], x2 = p.hcn[2], z2 = p.hcn[3];
                float yTran = z1 >= 20f ? caoS : caoP;               // phòng phía Nam nằm dưới trần sảnh 8 m
                int nx = Mathf.Max(1, Mathf.FloorToInt((x2 - x1) / 3.2f)), nz = Mathf.Max(1, Mathf.FloorToInt((z2 - z1) / 3.2f));
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float x = x1 + (x2 - x1) * (i + 0.5f) / nx, z = z1 + (z2 - z1) * (j + 0.5f) / nz;
                        Hop(goc, "KhungDen", new Vector3(x, yTran - 0.018f, z), new Vector3(1.3f, 0.035f, 0.7f), khung);
                        Hop(goc, "TamDenLED", new Vector3(x, yTran - 0.038f, z), new Vector3(1.2f, 0.012f, 0.6f), vlLed);
                    }
                for (float x = x1 + 1.2f; x < x2; x += 3.6f)
                    for (float z = z1 + 1.2f; z < z2; z += 3.6f)
                    {
                        Tru(goc, "DauPhun", new Vector3(x, yTran - 0.03f, z), 0.018f, 0.04f, vlDau);
                        Tru(goc, "DiaPhun", new Vector3(x, yTran - 0.004f, z), 0.045f, 0.004f, _vl.Trang);
                    }
            }
        }

        // ------------------------------------------------------------ 7. tường: cửa sổ nhôm, bình chữa cháy, đồng hồ
        /// <summary>Mỗi khung hình nhận texture đã tính xong ở luồng nền; xong hết thì tự gỡ.</summary>
        class BomTexture : MonoBehaviour
        {
            void Update()
            {
                VatLieuThuc.Bom();
                if (!VatLieuThuc.DangCho) Destroy(this);
            }
        }

        /// <summary>Tấm phẳng w × h (mét), nhìn từ -z, UV lặp theo mét (1,2 m = 1 chu kỳ texture gạch) để gạch không bị kéo giãn.</summary>
        static Mesh TamPhang(float w, float h)
        {
            var m = new Mesh { name = "TamGach" };
            m.vertices = new[] { new Vector3(-w / 2f, 0f, 0f), new Vector3(w / 2f, 0f, 0f), new Vector3(-w / 2f, h, 0f), new Vector3(w / 2f, h, 0f) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(w / 1.2f, 0f), new Vector2(0f, h / 1.2f), new Vector2(w / 1.2f, h / 1.2f) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Gạch men ốp chân tường (60 × 30 cm xếp so le, ron lõm, men bóng) + nẹp inox đỉnh: tường ngoài Tây / Đông / Nam / Bắc (phần dưới cửa sổ).</summary>
        static void GachOpTuong(DungToaNha d, Transform goc)
        {
            float rong = d.DuLieu.toa_nha.rong, sau = d.DuLieu.toa_nha.sau;
            const float DAY = 0.1755f;         // mặt trong tường dày 0,35 m
            const float Y0 = 0.12f;            // trên nẹp chân tường
            var vlNep = LabVatLieu.KimLoai(new Color(0.78f, 0.8f, 0.83f));
            void Tam(string ten, Vector3 tamChan, float w, float h, float yaw)
            {
                var g = new GameObject(ten);
                g.transform.SetParent(goc, false);
                g.transform.localPosition = tamChan;
                g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = TamPhang(w, h);
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = _vl.GachTuong;
                r.shadowCastingMode = ShadowCastingMode.Off;
                // nẹp inox mép trên
                var nep = Hop(g.transform, "NepInox", new Vector3(0f, h + 0.012f, -0.01f), new Vector3(w, 0.024f, 0.03f), vlNep);
                nep.transform.localRotation = Quaternion.identity;
            }
            // quay quanh y: Quad nhìn về -z; yaw 180 → +z (tường Bắc), yaw -90 → +x (tường Tây), yaw 90 → -x (tường Đông), yaw 0 → -z (tường Nam)
            Tam("GachBac", new Vector3(rong * 0.5f, Y0, DAY), rong, 1.2f - Y0, 180f);
            Tam("GachTay", new Vector3(DAY, Y0, sau * 0.5f), sau, 1.5f - Y0, -90f);
            Tam("GachDong", new Vector3(rong - DAY, Y0, sau * 0.5f), sau, 1.5f - Y0, 90f);
            float xCua = 18f, rongCua = 3.2f;
            float wTrai = xCua - rongCua * 0.5f, wPhai = rong - (xCua + rongCua * 0.5f);
            Tam("GachNamTrai", new Vector3(wTrai * 0.5f, Y0, sau - DAY), wTrai, 1.5f - Y0, 0f);
            Tam("GachNamPhai", new Vector3(rong - wPhai * 0.5f, Y0, sau - DAY), wPhai, 1.5f - Y0, 0f);
        }

        static void Tuong(DungToaNha d, Transform goc)
        {
            float rong = d.DuLieu.toa_nha.rong;
            // khung cửa sổ dải kính Bắc: kính x ∈ [1, 35], y ∈ [1,2 ; 4,2]
            for (int i = 0; i <= 17; i++) Hop(goc, "NepDungCuaSo", new Vector3(1f + 2f * i, 2.7f, 0.05f), new Vector3(0.06f, 3.0f, 0.07f), _vl.Nhom);
            foreach (float y in new[] { 1.2f, 2.7f, 4.2f }) Hop(goc, "NepNgangCuaSo", new Vector3(rong * 0.5f, y, 0.05f), new Vector3(rong - 2f, 0.07f, 0.07f), _vl.Nhom);
            Hop(goc, "BeCuaSo", new Vector3(rong * 0.5f, 1.215f, 0.17f), new Vector3(rong - 1.6f, 0.04f, 0.26f), _vl.Trang);
            // bình chữa cháy treo tường Tây (mặt trong tường x ≈ 0,175)
            foreach (float z in new[] { 10f, 19f, 26f })
            {
                var t = new GameObject("BinhChuaChay").transform;
                t.SetParent(goc, false);
                t.localPosition = new Vector3(0.2f, 0f, z);
                Tru(t, "Than", new Vector3(0.09f, 1.0f, 0f), 0.075f, 0.42f, _vl.Do);
                Tru(t, "Co", new Vector3(0.09f, 1.25f, 0f), 0.03f, 0.08f, _vl.Den);
                Hop(t, "TayGat", new Vector3(0.09f, 1.3f, 0f), new Vector3(0.09f, 0.018f, 0.03f), _vl.Den);
                Hop(t, "GiaDo", new Vector3(0.02f, 1.1f, 0f), new Vector3(0.03f, 0.05f, 0.2f), _vl.Den);
                Hop(t, "BienDo", new Vector3(0.012f, 1.55f, 0f), new Vector3(0.012f, 0.3f, 0.3f), _vl.Do);
            }
            // đồng hồ treo tường Đông (mặt trong tường x ≈ 35,825)
            var dh = new GameObject("DongHo").transform;
            dh.SetParent(goc, false);
            dh.localPosition = new Vector3(rong - 0.2f, 3.3f, 12f);
            dh.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Tru(dh, "Mat", Vector3.zero, 0.22f, 0.03f, _vl.Trang, new Vector3(90f, 0f, 0f));
            Tru(dh, "Vien", new Vector3(0f, 0f, 0.006f), 0.235f, 0.02f, _vl.Den, new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Hop(dh, "Vach", new Vector3(Mathf.Sin(a) * 0.19f, Mathf.Cos(a) * 0.19f, -0.018f), new Vector3(0.012f, 0.03f, 0.004f), _vl.Den).transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            }
            var kimGio = Hop(dh, "KimGio", new Vector3(-0.05f, 0.04f, -0.02f), new Vector3(0.016f, 0.12f, 0.004f), _vl.Den);
            kimGio.transform.localRotation = Quaternion.Euler(0f, 0f, 55f);
            var kimPhut = Hop(dh, "KimPhut", new Vector3(0.02f, 0.085f, -0.024f), new Vector3(0.01f, 0.18f, 0.004f), _vl.Den);
            kimPhut.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }
    }
}
