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
            var sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Hidden/InternalErrorShader");
            if (sh != null) return new Material(sh);

            // Fallback cuối cùng: lấy từ vật liệu mặc định của renderer
            var tempGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var defMat = tempGo.GetComponent<Renderer>().sharedMaterial;
            var res = defMat != null ? new Material(defMat) : null;
            Object.DestroyImmediate(tempGo);
            return res;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Material> _cache = new System.Collections.Generic.Dictionary<string, Material>();
        private static Material _matThuyTinhChung;

        /// <summary>Thuỷ tinh: trong suốt, rất bóng (phản chiếu môi trường tạo cảm giác khúc xạ), hiển thị hai mặt.</summary>
        public static Material ThuyTinh()
        {
            if (_matThuyTinhChung == null)
            {
                _matThuyTinhChung = Goc("ThuyTinh");
                DatTrongSuot(_matThuyTinhChung, new Color(0.88f, 0.94f, 1f, 0.22f), 0.96f);
            }
            return _matThuyTinhChung;
        }

        public static Material Long(Color c)
        {
            string key = $"long_{c.r:F2}_{c.g:F2}_{c.b:F2}_{c.a:F2}";
            if (_cache.TryGetValue(key, out var daCo) && daCo != null) return daCo;
            var m = Goc("Long");
            DatTrongSuot(m, c, 0.85f);
            _cache[key] = m;
            return m;
        }

        public static Material Dac(Color c, float bong = 0.35f)
        {
            string key = $"dac_{c.r:F2}_{c.g:F2}_{c.b:F2}_{c.a:F2}_{bong:F2}";
            if (_cache.TryGetValue(key, out var daCo) && daCo != null) return daCo;
            var m = Goc("Dac");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", bong);
            _cache[key] = m;
            return m;
        }

        public static Material KimLoai(Color c)
        {
            string key = $"kl_{c.r:F2}_{c.g:F2}_{c.b:F2}";
            if (_cache.TryGetValue(key, out var daCo) && daCo != null) return daCo;
            var m = Goc("KimLoai");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0.9f);
            m.SetFloat("_Smoothness", 0.7f);
            _cache[key] = m;
            return m;
        }

        /// <summary>Hạt phát sáng cộng màu (electron, ngọn lửa, bọt khí).</summary>
        public static Material PhatSang(Color c)
        {
            string key = $"ps_{c.r:F2}_{c.g:F2}_{c.b:F2}";
            if (_cache.TryGetValue(key, out var daCo) && daCo != null) return daCo;
            var m = Goc("PhatSang");
            m.SetColor("_BaseColor", c);
            _cache[key] = m;
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
            if (m == null) return;
            m.SetColor("_BaseColor", c);
            if (_haiLuot.TryGetValue(m, out var hai)) foreach (var x in hai) if (x != null) x.SetColor("_BaseColor", c);
        }

        static readonly System.Collections.Generic.Dictionary<Material, Material[]> _haiLuot = new System.Collections.Generic.Dictionary<Material, Material[]>();

        /// <summary>Trong suốt vẽ HAI LƯỢT cho lưới kín (thuỷ tinh, chất lỏng): [0] = mặt sau (cull Front), [1] = mặt trước (cull Back).
        /// Vật liệu trong suốt không ghi chiều sâu, nếu vẽ cả hai mặt trong một lượt thì mặt trước / sau của cùng lưới trộn màu theo thứ tự
        /// tam giác → vệt răng cưa lặp theo từng vòng ngang. Dùng với lưới có 2 submesh dùng chung tam giác (LabHinh.* haiLuot).
        /// DatMau(goc, màu) cập nhật cả hai bản.</summary>
        public static Material[] HaiLuot(Material goc)
        {
            if (_haiLuot.TryGetValue(goc, out var cu) && cu[0] != null && cu[1] != null) return cu;
            var sau = new Material(goc) { name = goc.name + "_sau" };
            sau.SetFloat("_Cull", (float)CullMode.Front);
            var truoc = new Material(goc) { name = goc.name + "_truoc" };
            truoc.SetFloat("_Cull", (float)CullMode.Back);
            return _haiLuot[goc] = new[] { sau, truoc };
        }
    }
}
