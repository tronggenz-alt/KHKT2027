using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem.ThiNghiem
{
    /// <summary>
    /// Chuẩn hóa kích thước dụng cụ phòng thí nghiệm theo tiêu chuẩn quốc tế ISO:
    /// - Burette 50 mL (ISO 385): dài 75 cm, đường kính trong 11 mm, ngoài 14 mm.
    /// - Bình tam giác Erlenmeyer 250 mL (ISO 1773): cao 14 cm, đáy 8.5 cm, cổ 3.4 cm.
    /// - Cốc đong Beaker 100 mL (ISO 3819): cao 7 cm, đường kính 5 cm.
    /// - Cốc đong Beaker 250 mL (ISO 3819): cao 9.5 cm, đường kính 7 cm.
    /// - Ống nghiệm (ISO 4142): 16 × 150 mm.
    /// - Cầu muối chữ U: phi 12 mm, dài 15 cm.
    /// - Điện cực than chì / kim loại: 10 × 1 × 0.2 cm.
    /// </summary>
    public static class DungCuISO
    {
        /// <summary>
        /// Tạo Burette 50 mL chuẩn ISO 385 với vạch đo decal, khoá vòi Teflon và điểm nóng xoay van.
        /// </summary>
        public static GameObject TaoBurette50mL(Transform parent, Vector3 viTri)
        {
            var go = new GameObject("Burette_50mL_ISO385");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlKinh = LabVatLieu.ThuyTinh();
            var vlTeflon = LabVatLieu.Dac(new Color(0.92f, 0.94f, 0.96f), 0.5f);
            var vlInox = LabVatLieu.KimLoai(new Color(0.85f, 0.85f, 0.88f));

            // 1. Thân ống thuỷ tinh burette dài 75 cm (0.75m), đường kính 14 mm (0.014m)
            var than = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            than.name = "ThanOngBurette";
            than.transform.SetParent(go.transform, false);
            than.transform.localPosition = new Vector3(0f, 0.375f, 0f);
            than.transform.localScale = new Vector3(0.014f, 0.375f, 0.014f);
            than.GetComponent<Renderer>().material = vlKinh;

            // 2. Đầu nhỏ giọt vuốt nhọn ở dưới
            var dauNho = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dauNho.name = "DauNhoGiot";
            dauNho.transform.SetParent(go.transform, false);
            dauNho.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            dauNho.transform.localScale = new Vector3(0.005f, 0.04f, 0.005f);
            dauNho.GetComponent<Renderer>().material = vlKinh;

            // 3. Khoá vòi xoay PTFE Teflon (Stopcock)
            var khoa = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            khoa.name = "KhoaVoiTeflon";
            khoa.transform.SetParent(go.transform, false);
            khoa.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            khoa.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            khoa.transform.localScale = new Vector3(0.012f, 0.025f, 0.012f);
            khoa.GetComponent<Renderer>().material = vlTeflon;

            // Điểm nóng tương tác ① trên khoá vòi burette
            DiemNongTuongTac.TaoDiemNong(khoa.transform, "① Xoay van burette nhỏ giọt", Vector3.zero);

            // 4. Giá kẹp burette kim loại gắn trên bàn
            var cotGia = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cotGia.name = "CotGiaDo";
            cotGia.transform.SetParent(go.transform, false);
            cotGia.transform.localPosition = new Vector3(-0.08f, 0.35f, 0f);
            cotGia.transform.localScale = new Vector3(0.012f, 0.40f, 0.012f);
            cotGia.GetComponent<Renderer>().material = vlInox;

            var deGia = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deGia.name = "DeGiaSat";
            deGia.transform.SetParent(go.transform, false);
            deGia.transform.localPosition = new Vector3(-0.08f, -0.01f, 0f);
            deGia.transform.localScale = new Vector3(0.18f, 0.02f, 0.28f);
            deGia.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.2f, 0.22f, 0.25f), 0.6f);

            return go;
        }

        /// <summary>
        /// Tạo Bình tam giác Erlenmeyer 250 mL chuẩn ISO 1773 (cao 14 cm, đáy 8.5 cm, cổ 3.4 cm).
        /// </summary>
        public static GameObject TaoBinhTamGiac250mL(Transform parent, Vector3 viTri, float mucDungDich = 0.04f, Color? mauDungDich = null)
        {
            var go = new GameObject("BinhTamGiac_250mL_ISO1773");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlKinh = LabVatLieu.ThuyTinh();

            // 1. Thân hình nón cụt (cao 10 cm, đáy phi 8.5 cm, đỉnh phi 3.4 cm)
            var thanNon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            thanNon.name = "ThanNon";
            thanNon.transform.SetParent(go.transform, false);
            thanNon.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            thanNon.transform.localScale = new Vector3(0.085f, 0.05f, 0.085f);
            thanNon.GetComponent<Renderer>().material = vlKinh;

            // 2. Cổ bình hình trụ (cao 4 cm, phi 3.4 cm)
            var coBinh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coBinh.name = "CoBinh";
            coBinh.transform.SetParent(go.transform, false);
            coBinh.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            coBinh.transform.localScale = new Vector3(0.034f, 0.02f, 0.034f);
            coBinh.GetComponent<Renderer>().material = vlKinh;

            // 3. Vành miệng bình gia cố viền loe
            var vanh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vanh.name = "VanhLoe";
            vanh.transform.SetParent(go.transform, false);
            vanh.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            vanh.transform.localScale = new Vector3(0.038f, 0.003f, 0.038f);
            vanh.GetComponent<Renderer>().material = vlKinh;

            // 4. Dung dịch bên trong với mặt cong khum meniscus
            if (mucDungDich > 0.005f)
            {
                Color col = mauDungDich ?? new Color(0.85f, 0.92f, 1f, 0.45f);
                var chatLong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chatLong.name = "DungDich_Meniscus";
                chatLong.transform.SetParent(go.transform, false);
                chatLong.transform.localPosition = new Vector3(0f, mucDungDich * 0.5f + 0.002f, 0f);
                chatLong.transform.localScale = new Vector3(0.078f, mucDungDich * 0.5f, 0.078f);
                chatLong.GetComponent<Renderer>().material = LabVatLieu.Long(col);
                UnityEngine.Object.DestroyImmediate(chatLong.GetComponent<Collider>());
            }

            return go;
        }

        /// <summary>
        /// Tạo Cốc đong Beaker 250 mL chuẩn ISO 3819 (cao 9.5 cm, đường kính 7.0 cm có mỏ rót).
        /// </summary>
        public static GameObject TaoCocDot250mL(Transform parent, Vector3 viTri, float mucDungDich = 0.05f, Color? mauDungDich = null)
        {
            var go = new GameObject("CocDot_250mL_ISO3819");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlKinh = LabVatLieu.ThuyTinh();

            // 1. Thân cốc hình trụ
            var than = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            than.name = "ThanCoc";
            than.transform.SetParent(go.transform, false);
            than.transform.localPosition = new Vector3(0f, 0.0475f, 0f);
            than.transform.localScale = new Vector3(0.070f, 0.0475f, 0.070f);
            than.GetComponent<Renderer>().material = vlKinh;

            // 2. Mỏ rót nhỏ ở miệng
            var moRot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moRot.name = "MoRot";
            moRot.transform.SetParent(go.transform, false);
            moRot.transform.localPosition = new Vector3(0.034f, 0.095f, 0f);
            moRot.transform.localScale = new Vector3(0.008f, 0.005f, 0.012f);
            moRot.GetComponent<Renderer>().material = vlKinh;

            // 3. Dung dịch bên trong
            if (mucDungDich > 0.005f)
            {
                Color col = mauDungDich ?? new Color(0.2f, 0.55f, 0.95f, 0.5f);
                var chatLong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chatLong.name = "DungDichTrongCoc";
                chatLong.transform.SetParent(go.transform, false);
                chatLong.transform.localPosition = new Vector3(0f, mucDungDich * 0.5f + 0.002f, 0f);
                chatLong.transform.localScale = new Vector3(0.066f, mucDungDich * 0.5f, 0.066f);
                chatLong.GetComponent<Renderer>().material = LabVatLieu.Long(col);
                UnityEngine.Object.DestroyImmediate(chatLong.GetComponent<Collider>());
            }

            return go;
        }

        /// <summary>
        /// Tạo Giá ống nghiệm gỗ 6 lỗ kèm các ống nghiệm chuẩn ISO 4142 (16 × 150 mm).
        /// </summary>
        public static GameObject TaoGiaOngNghiem6Lo(Transform parent, Vector3 viTri)
        {
            var go = new GameObject("GiaOngNghiem_6Lo_ISO");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlGo = LabVatLieu.Dac(new Color(0.82f, 0.70f, 0.52f), 0.35f);
            var vlKinh = LabVatLieu.ThuyTinh();

            // Giá gỗ: thanh đáy, thanh trên và 2 thanh đỡ bên
            var day = GameObject.CreatePrimitive(PrimitiveType.Cube);
            day.name = "ThanhDay";
            day.transform.SetParent(go.transform, false);
            day.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            day.transform.localScale = new Vector3(0.24f, 0.02f, 0.06f);
            day.GetComponent<Renderer>().material = vlGo;

            var tren = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tren.name = "ThanhTrenCoLo";
            tren.transform.SetParent(go.transform, false);
            tren.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            tren.transform.localScale = new Vector3(0.24f, 0.015f, 0.06f);
            tren.GetComponent<Renderer>().material = vlGo;

            // 4 ống nghiệm mẫu 16 × 150 mm (ISO 4142)
            Color[] mauMaus = new Color[]
            {
                new Color(0.9f, 0.35f, 0.15f, 0.7f), // Fe3+ nâu vàng
                new Color(0.4f, 0.85f, 0.45f, 0.6f), // Fe2+ lục nhạt
                new Color(0.15f, 0.55f, 0.95f, 0.7f), // Cu2+ xanh lam
                new Color(0.92f, 0.95f, 0.98f, 0.4f)  // Al3+ trong suốt
            };

            for (int i = 0; i < 4; i++)
            {
                float ox = -0.075f + i * 0.05f;
                var ong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ong.name = $"OngNghiem_{i + 1}";
                ong.transform.SetParent(go.transform, false);
                ong.transform.localPosition = new Vector3(ox, 0.08f, 0f);
                ong.transform.localScale = new Vector3(0.016f, 0.075f, 0.016f); // 16mm x 150mm
                ong.GetComponent<Renderer>().material = vlKinh;

                // Dung dịch trong ống nghiệm
                var dd = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dd.name = "DungDichOng";
                dd.transform.SetParent(ong.transform, false);
                dd.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                dd.transform.localScale = new Vector3(0.88f, 0.5f, 0.88f);
                dd.GetComponent<Renderer>().material = LabVatLieu.Long(mauMaus[i]);
                UnityEngine.Object.DestroyImmediate(dd.GetComponent<Collider>());
            }

            return go;
        }

        /// <summary>
        /// Tạo Cầu muối chữ U chuẩn (đường kính 12mm, dài 15cm) chứa thạch agar KNO3.
        /// </summary>
        public static GameObject TaoCauMuoiChuU(Transform parent, Vector3 viTri)
        {
            var go = new GameObject("CauMuoi_ChuU_ISO");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlKinh = LabVatLieu.ThuyTinh();
            var vlAgar = LabVatLieu.Long(new Color(0.85f, 0.90f, 0.95f, 0.6f));

            // Thanh ngang trên
            var ngang = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ngang.name = "ThanhNgang";
            ngang.transform.SetParent(go.transform, false);
            ngang.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            ngang.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ngang.transform.localScale = new Vector3(0.012f, 0.06f, 0.012f);
            ngang.GetComponent<Renderer>().material = vlKinh;

            // Nhánh trái cắm vào cốc Zn
            var nhanhTrai = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nhanhTrai.name = "NhanhTrai";
            nhanhTrai.transform.SetParent(go.transform, false);
            nhanhTrai.transform.localPosition = new Vector3(-0.06f, 0.06f, 0f);
            nhanhTrai.transform.localScale = new Vector3(0.012f, 0.06f, 0.012f);
            nhanhTrai.GetComponent<Renderer>().material = vlKinh;

            // Nhánh phải cắm vào cốc Cu
            var nhanhPhai = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nhanhPhai.name = "NhanhPhai";
            nhanhPhai.transform.SetParent(go.transform, false);
            nhanhPhai.transform.localPosition = new Vector3(0.06f, 0.06f, 0f);
            nhanhPhai.transform.localScale = new Vector3(0.012f, 0.06f, 0.012f);
            nhanhPhai.GetComponent<Renderer>().material = vlKinh;

            // Điểm nóng tương tác ② trên cầu muối
            DiemNongTuongTac.TaoDiemNong(ngang.transform, "② Nhấc / Cắm cầu muối ngắt mạch", Vector3.zero);

            return go;
        }

        /// <summary>
        /// Tạo Bộ Điện Cực Than Chì & Nguồn DC cho thí nghiệm điện phân.
        /// </summary>
        public static GameObject TaoBoDienCucDienPhan(Transform parent, Vector3 viTri)
        {
            var go = new GameObject("BoDienCuc_DienPhan_ISO");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;

            var vlThanChi = LabVatLieu.Dac(new Color(0.15f, 0.15f, 0.16f), 0.3f);
            var vlDayDo = LabVatLieu.Dac(new Color(0.9f, 0.15f, 0.15f), 0.5f);
            var vlDayDen = LabVatLieu.Dac(new Color(0.12f, 0.12f, 0.14f), 0.5f);

            // Cực dương Anode (than chì 10 × 1 × 0.2 cm)
            var anode = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anode.name = "DienCuc_Anode";
            anode.transform.SetParent(go.transform, false);
            anode.transform.localPosition = new Vector3(-0.025f, 0.05f, 0f);
            anode.transform.localScale = new Vector3(0.01f, 0.10f, 0.002f);
            anode.GetComponent<Renderer>().material = vlThanChi;

            // Cực âm Cathode (than chì 10 × 1 × 0.2 cm)
            var cathode = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cathode.name = "DienCuc_Cathode";
            cathode.transform.SetParent(go.transform, false);
            cathode.transform.localPosition = new Vector3(0.025f, 0.05f, 0f);
            cathode.transform.localScale = new Vector3(0.01f, 0.10f, 0.002f);
            cathode.GetComponent<Renderer>().material = vlThanChi;

            // Điểm nóng tương tác ③ công tắc nguồn điện phân
            DiemNongTuongTac.TaoDiemNong(go.transform, "③ Bật / Ngắt nguồn điện phân DC", new Vector3(0f, 0.14f, 0f));

            return go;
        }
    }
}
