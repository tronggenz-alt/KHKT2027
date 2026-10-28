// Cảnh 2 – An toàn phòng thí nghiệm: bàn "tìm lỗi" với 12 điểm đánh số (8 lỗi + 4 việc làm đúng) bấm được.
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem
{
    public class CanhAnToan : CanhCoSo
    {
        readonly Dictionary<string, Vector3> _viTri = new Dictionary<string, Vector3>
        {
            { "lo_khong_nhan", new Vector3(-0.77f, 0.2f, -0.35f) }, { "khong_kinh", new Vector3(-0.55f, 0.09f, 0.25f) },
            { "do_nuoc_vao_acid", new Vector3(-0.27f, 0.24f, -0.05f) }, { "ngui_truc_tiep", new Vector3(0.13f, 0.42f, -0.36f) },
            { "huong_mieng_ong", new Vector3(0.5f, 0.3f, 0.05f) }, { "an_uong", new Vector3(0.87f, 0.13f, -0.23f) },
            { "cam_tay", new Vector3(-0.12f, 0.12f, 0.3f) }, { "den_khong_nguoi", new Vector3(1.05f, 0.22f, 0.3f) },
            { "tu_hut", new Vector3(-1.0f, 0.56f, 0.3f) }, { "binh_cuu_hoa", new Vector3(1.2f, 0.3f, -0.38f) },
            { "thung_thai", new Vector3(0.55f, 0.22f, -0.42f) }, { "gang_tay", new Vector3(-0.6f, 0.12f, -0.3f) },
        };
        readonly Dictionary<string, (Material vl, TextMesh chu)> _diem = new Dictionary<string, (Material, TextMesh)>();

        protected override void DungCanh(string tt)
        {
            GocViTri = new Vector3(0.1f, 1.6f, 2.4f);
            GocDich = new Vector3(0.08f, 0.05f, 0f);
            GocGan = 0.6f; GocXa = 4.5f;
            var da = LabVatLieu.Dac(new Color(0.95f, 0.79f, 0.65f), 0.3f);
            // lọ hoá chất (một lọ không nhãn)
            for (int i = 0; i < 3; i++)
            {
                float x = -0.95f + i * 0.09f;
                LuoiGO("Lo", LabHinh.Lathe(LabHinh.LoThuoc(0.03f, 0.12f, 0.0135f), 64),
                    LabVatLieu.ThuyTinh(), new Vector3(x, 0f, -0.35f));
                Tru(new Vector3(x, 0.005f, -0.35f), 0.026f, 0.06f, LabVatLieu.Long(i == 1 ? new Color(0.27f, 0.59f, 0.9f, 0.75f) : new Color(0.92f, 0.9f, 0.75f, 0.7f)));
                if (i < 2) Tru(new Vector3(x, 0.035f, -0.35f), 0.0305f, 0.03f, LabVatLieu.Dac(Color.white));
            }
            Chu("NaCl", new Vector3(-0.95f, 0.15f, -0.35f)); Chu("CuSO₄", new Vector3(-0.86f, 0.15f, -0.35f));
            // kính bảo hộ để trên bàn
            Khoi(PrimitiveType.Cylinder, new Vector3(-0.585f, 0.008f, 0.25f), new Vector3(0.06f, 0.006f, 0.06f), LabVatLieu.Dac(MauNhan));
            Khoi(PrimitiveType.Cylinder, new Vector3(-0.515f, 0.008f, 0.25f), new Vector3(0.06f, 0.006f, 0.06f), LabVatLieu.Dac(MauNhan));
            // cốc H₂SO₄ đặc + bình nước nghiêng đổ vào
            LuoiGO("Coc acid", LabHinh.Lathe(LabHinh.Coc(0.04f, 0.09f), 64), LabVatLieu.ThuyTinh(), new Vector3(-0.3f, 0f, -0.05f));
            Tru(new Vector3(-0.3f, 0.003f, -0.05f), 0.037f, 0.045f, LabVatLieu.Long(new Color(0.96f, 0.94f, 0.88f, 0.55f)));
            Chu("H₂SO₄ đặc", new Vector3(-0.3f, 0.13f, -0.05f));
            var binhNuoc = LuoiGO("Binh nuoc", LabHinh.Lathe(LabHinh.Coc(0.032f, 0.1f), 64), LabVatLieu.ThuyTinh(), new Vector3(-0.24f, 0.14f, -0.05f));
            binhNuoc.transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
            // giá ống nghiệm + học sinh cúi ngửi
            Khoi(PrimitiveType.Cube, new Vector3(0.12f, 0.06f, -0.2f), new Vector3(0.2f, 0.012f, 0.06f), LabVatLieu.Dac(new Color(0.84f, 0.76f, 0.65f)));
            for (int i = 0; i < 4; i++)
                LuoiGO("Ong", LabHinh.Lathe(LabHinh.OngNghiem(0.009f, 0.12f), 40), LabVatLieu.ThuyTinh(), new Vector3(0.05f + i * 0.045f, 0.01f, -0.2f));
            Nguoi(new Vector3(0.13f, 0f, -0.42f), new Color(0.36f, 0.5f, 0.65f), 18f, da);
            // ống nghiệm đun trên đèn cồn hướng vào bạn khác
            DenCon(new Vector3(0.45f, 0f, 0.05f));
            var ongDun = LuoiGO("Ong dun", LabHinh.Lathe(LabHinh.OngNghiem(0.01f, 0.13f), 40), LabVatLieu.ThuyTinh(), new Vector3(0.45f, 0.16f, 0.05f));
            ongDun.transform.localRotation = Quaternion.Euler(0f, -30f, -57f);
            Nguoi(new Vector3(0.72f, 0f, 0.35f), new Color(0.54f, 0.43f, 0.69f), 0f, da);
            // đồ ăn uống
            Tru(new Vector3(0.82f, 0f, -0.25f), 0.03f, 0.08f, LabVatLieu.Dac(new Color(0.98f, 0.45f, 0.09f)));
            Khoi(PrimitiveType.Cube, new Vector3(0.93f, 0.016f, -0.22f), new Vector3(0.1f, 0.03f, 0.06f), LabVatLieu.Dac(new Color(0.85f, 0.64f, 0.36f)));
            // đĩa bột hoá chất + bàn tay trần
            Tru(new Vector3(-0.12f, 0f, 0.32f), 0.06f, 0.01f, LabVatLieu.Dac(new Color(0.97f, 0.98f, 0.99f)));
            Khoi(PrimitiveType.Sphere, new Vector3(-0.12f, 0.018f, 0.32f), new Vector3(0.06f, 0.025f, 0.06f), LabVatLieu.Dac(new Color(0.31f, 0.6f, 0.85f)));
            Khoi(PrimitiveType.Cube, new Vector3(-0.12f, 0.05f, 0.24f), new Vector3(0.05f, 0.016f, 0.06f), da);
            // đèn cồn cuối bàn không người trông
            DenCon(new Vector3(1.05f, 0f, 0.3f));
            // việc làm đúng: tủ hút, bình chữa cháy, thùng chất thải, hộp găng tay
            Khoi(PrimitiveType.Cube, new Vector3(-1.0f, 0.25f, 0.25f), new Vector3(0.42f, 0.5f, 0.3f), LabVatLieu.Dac(new Color(0.87f, 0.9f, 0.93f)));
            Tru(new Vector3(1.2f, 0f, -0.38f), 0.04f, 0.24f, LabVatLieu.Dac(new Color(0.86f, 0.15f, 0.15f), 0.6f));
            Tru(new Vector3(0.55f, 0f, -0.42f), 0.07f, 0.16f, LabVatLieu.Dac(new Color(0.92f, 0.7f, 0.03f)));
            Khoi(PrimitiveType.Cube, new Vector3(-0.6f, 0.03f, -0.3f), new Vector3(0.12f, 0.06f, 0.08f), LabVatLieu.Dac(new Color(0.38f, 0.65f, 0.98f)));

            var s = JsonUtility.FromJson<TTAnToan>(tt);
            if (s?.diem_nong == null) return;
            foreach (var d in s.diem_nong)
            {
                if (!_viTri.TryGetValue(d.id, out var vt)) continue;
                var vl = LabVatLieu.Dac(MauNhan, 0.5f);
                var g = Khoi(PrimitiveType.Sphere, vt, Vector3.one * 0.05f, vl, null, true);
                var n = g.AddComponent<LabNhap>();
                n.lenh = "bam_diem";
                n.thamSo = "{\"id\":\"" + d.id + "\"}";
                var chu = Chu(d.so.ToString(), vt + new Vector3(0f, 0.055f, 0f), 0.0045f);   // số nằm trên quả cầu, không bị che
                chu.fontStyle = FontStyle.Bold;
                _diem[d.id] = (vl, chu);
            }
        }

        void Nguoi(Vector3 vt, Color ao, float nghieng, Material da)
        {
            var g = Moi("Nguoi").transform;
            g.localPosition = vt;
            g.localRotation = Quaternion.Euler(nghieng, 0f, 0f);
            Khoi(PrimitiveType.Capsule, new Vector3(0f, 0.2f, 0f), new Vector3(0.15f, 0.185f, 0.15f), LabVatLieu.Dac(ao), g);
            Khoi(PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.12f, da, g);
        }

        void DenCon(Vector3 vt)
        {
            LuoiGO("Den con", LabHinh.Lathe(LabHinh.DenCon(), 64),
                LabVatLieu.ThuyTinh(), vt);
            var lua = Hat(Goc, vt + new Vector3(0f, 0.095f, 0f), new Color(1f, 0.72f, 0.25f, 0.9f), 0.03f, 0.08f, 0.35f, 0.004f, 40);
            DatPhat(lua, 60f);
            NgonLua.GanVao(lua);   // ngọn lửa thật (shader PhongLab/NgonLua) thay tấm hạt vuông phẳng
        }

        public override void CapNhat(string json)
        {
            var s = JsonUtility.FromJson<TTAnToan>(json);
            if (s?.diem_nong == null) return;
            foreach (var d in s.diem_nong)
            {
                if (!_diem.TryGetValue(d.id, out var x)) continue;
                LabVatLieu.DatMau(x.vl, d.da_tim ? new Color(0.09f, 0.64f, 0.29f) : d.la_viec_dung ? new Color(0.45f, 0.47f, 0.5f) : MauNhan);
                x.chu.text = KyTu(d.da_tim ? "✓" : d.so.ToString());
            }
        }
    }
}
