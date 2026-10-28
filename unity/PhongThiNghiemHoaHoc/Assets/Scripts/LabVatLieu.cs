// Vật liệu URP dựng lúc chạy, sao từ vật liệu mẫu trong Resources/PTN (BuildWebGL tạo sẵn để shader được đưa vào bản build).
using UnityEngine;
using UnityEngine.Rendering;

namespace PhongThiNghiem
{
    public static class LabVatLieu
    {
        static Material Goc(string ten)
        {
            var mau = Resources.Load<Material>("PTN/" + ten);
            if (mau != null) return new Material(mau);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            return new Material(sh);
        }

        /// <summary>Thuỷ tinh: trong suốt, rất bóng (phản chiếu môi trường tạo cảm giác khúc xạ), hiển thị hai mặt.</summary>
        public static Material ThuyTinh()
        {
            var m = Goc("ThuyTinh");
            DatTrongSuot(m, new Color(0.88f, 0.94f, 1f, 0.2f), 0.96f);
            return m;
        }

        public static Material Long(Color c)
        {
            var m = Goc("Long");
            DatTrongSuot(m, c, 0.85f);
            return m;
        }

        public static Material Dac(Color c, float bong = 0.35f)
        {
            var m = Goc("Dac");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", bong);
            return m;
        }

        public static Material KimLoai(Color c)
        {
            var m = Goc("KimLoai");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0.9f);
            m.SetFloat("_Smoothness", 0.7f);
            return m;
        }

        /// <summary>Hạt phát sáng cộng màu (electron, ngọn lửa, bọt khí).</summary>
        public static Material PhatSang(Color c)
        {
            var m = Goc("PhatSang");
            m.SetColor("_BaseColor", c);
            return m;
        }

        public static void DatTrongSuot(Material m, Color c, float bong)
        {
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetFloat("_Smoothness", bong);
        }

        public static void DatMau(Material m, Color c)
        {
            if (m != null) m.SetColor("_BaseColor", c);
        }
    }
}
