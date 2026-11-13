using UnityEngine;
using UnityEngine.Rendering;
using Khkt.Environment;
using Khkt.Interaction.Bio;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// Khung cổng liên kết 3D của BioPortal (2026-11-13). Trước đây "cổng" chỉ là một vòng sáng trên sàn và một thẻ mời,
    /// người dùng không thấy cổng nào để bước qua. Nay có một khung cổng thật đứng trên vòng sáng:
    ///   • hai trụ và xà ngang kim loại tối, góc vát gia cường, dải đèn lam ngọc chạy dọc mép trong;
    ///   • màng năng lượng trong lòng cổng (lưới lục giác trôi chậm, sáng hơn khi lại gần);
    ///   • biển tên "CƠ THỂ NGƯỜI 3D" phía trên.
    /// Bước qua lòng cổng là vào phòng (không cần bấm), vẫn giữ thẻ mời và phím E cho ai thích bấm.
    /// Cổng dựng bằng mã lúc chạy nên cảnh đã lưu từ trước cũng có cổng, không cần dựng lại cảnh.
    /// </summary>
    public partial class BioPortal
    {
        [Header("Khung cổng 3D")]
        public float gateWidth = 1.64f;
        public float gateHeight = 2.30f;
        [Tooltip("Bước qua lòng cổng thì tự vào phòng")]
        public bool walkThroughEnters = true;

        Transform gateRoot;
        Material gateFrameMat, gateGlowMat, gateFieldMat;
        Texture2D gateFieldTex;
        MeshRenderer gateFieldRenderer;
        float fieldLevel = 0.35f;
        bool walkArmed;

        /// <summary>Hướng từ cổng ra chỗ người dùng nên đứng (mặt trước cổng).</summary>
        public Vector3 StandDirection => transform.forward;

        /// <summary>Tâm lòng cổng (cao ngang ngực), dùng để tính khoảng cách và góc nhìn.</summary>
        public Vector3 GateCenter => transform.position + Vector3.up * 1.2f;

        public void EnsureGate()
        {
            if (gateRoot != null) return;
            var old = transform.Find("KhungCong");
            if (old != null) Destroy(old.gameObject);

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || unlit == null) return;

            gateRoot = new GameObject("KhungCong").transform;
            gateRoot.SetParent(transform, false);

            gateFrameMat = new Material(lit) { name = "CongKhung", hideFlags = HideFlags.DontSave };
            gateFrameMat.SetColor("_BaseColor", new Color(0.085f, 0.10f, 0.13f));
            gateFrameMat.SetFloat("_Metallic", 0.55f);
            gateFrameMat.SetFloat("_Smoothness", 0.5f);

            gateGlowMat = new Material(unlit) { name = "CongDaiDen", hideFlags = HideFlags.DontSave };
            gateGlowMat.SetColor("_BaseColor", new Color(0.25f, 0.95f, 1.25f));

            float w = gateWidth, h = gateHeight;
            const float post = 0.16f, depth = 0.24f;
            float px = w * 0.5f + post * 0.5f;

            // hai trụ (có va chạm: không đi xuyên qua trụ), chân đế, xà ngang
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Trụ", new Vector3(side * px, h * 0.5f, 0), new Vector3(post, h, depth), gateFrameMat, true);
                Part("Chân đế", new Vector3(side * px, 0.03f, 0), new Vector3(post + 0.18f, 0.06f, depth + 0.20f), gateFrameMat, false);
                // góc vát gia cường ở hai góc trên
                var gusset = Part("Góc vát", new Vector3(side * (w * 0.5f - 0.17f), h - 0.17f, 0), new Vector3(0.52f, 0.07f, depth * 0.8f), gateFrameMat, false);
                gusset.localRotation = Quaternion.Euler(0, 0, side * 45f);
                // dải đèn dọc mép trong của trụ (mặt trước và mặt sau)
                foreach (float z in new[] { depth * 0.5f + 0.004f, -depth * 0.5f - 0.004f })
                    Part("Dải đèn trụ", new Vector3(side * (px - post * 0.5f + 0.022f), h * 0.5f, z), new Vector3(0.022f, h - 0.5f, 0.008f), gateGlowMat, false);
            }
            Part("Xà ngang", new Vector3(0, h + post * 0.5f, 0), new Vector3(w + post * 2f, post, depth), gateFrameMat, false);
            foreach (float z in new[] { depth * 0.5f + 0.004f, -depth * 0.5f - 0.004f })
                Part("Dải đèn xà", new Vector3(0, h + 0.022f, z), new Vector3(w - 0.7f, 0.022f, 0.008f), gateGlowMat, false);

            // biển tên phía trên xà
            Part("Biển tên", new Vector3(0, h + post + 0.17f, 0), new Vector3(1.24f, 0.28f, 0.05f), gateFrameMat, false);
            Sign("CƠ THỂ NGƯỜI 3D", new Vector3(0, h + post + 0.20f, 0.028f), 0f, 0.055f, new Color(0.55f, 0.95f, 1f));
            Sign("PHÒNG SINH HỌC ẢO · BodyParts3D", new Vector3(0, h + post + 0.085f, 0.028f), 0f, 0.022f, new Color(1f, 0.74f, 0.32f));
            Sign("CƠ THỂ NGƯỜI 3D", new Vector3(0, h + post + 0.17f, -0.028f), 180f, 0.055f, new Color(0.55f, 0.95f, 1f));

            // màng năng lượng trong lòng cổng
            gateFieldTex = BuildFieldTexture(256);
            gateFieldMat = CreateAdditiveMaterial(gateFieldTex);
            gateFieldMat.name = "CongMangNangLuong";
            var field = new GameObject("Màng năng lượng");
            field.transform.SetParent(gateRoot, false);
            field.transform.localPosition = new Vector3(0, h * 0.5f, 0);
            var mf = field.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildVerticalQuad(w, h);
            gateFieldRenderer = field.AddComponent<MeshRenderer>();
            gateFieldRenderer.sharedMaterial = gateFieldMat;
            gateFieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            gateFieldRenderer.receiveShadows = false;
            gateFieldRenderer.lightProbeUsage = LightProbeUsage.Off;
            gateFieldRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        Transform Part(string name, Vector3 localPos, Vector3 size, Material mat, bool collide)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(gateRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (!collide)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            bool glow = mat == gateGlowMat;
            r.shadowCastingMode = glow ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows = !glow;
            return go.transform;
        }

        void Sign(string text, Vector3 localPos, float yaw, float size, Color color)
        {
            var go = new GameObject("Chữ biển");
            go.transform.SetParent(gateRoot, false);
            go.transform.localPosition = localPos;
            // TextMesh đọc được khi nhìn dọc theo trục z của nó: mặt trước cổng là +z nên chữ quay 180°
            go.transform.localRotation = Quaternion.Euler(0, yaw + 180f, 0);
            var tm = go.AddComponent<TextMesh>();
            var font = GlassKit.Semibold;
            tm.font = font;
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = size * 0.16f;
            tm.color = color;
            var r = go.GetComponent<MeshRenderer>();
            var inScene = Shader.Find("Khkt/ChuTrongCanh");   // chữ có kiểm tra độ sâu, không hiện xuyên tường
            if (inScene != null)
            {
                var m = new Material(inScene) { hideFlags = HideFlags.DontSave };
                m.mainTexture = font.material.mainTexture;
                r.sharedMaterial = m;
            }
            else r.sharedMaterial = font.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        Mesh BuildVerticalQuad(float w, float h)
        {
            var m = new Mesh { name = "BioPortalGateField" };
            float hw = w * 0.5f, hh = h * 0.5f;
            m.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.uv = new[] { new Vector2(0, 0), new Vector2(w / h, 0), new Vector2(w / h, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Ảnh lưới lục giác lặp được theo chiều dọc: viền ô sáng lam ngọc, lòng ô gần trong suốt.</summary>
        Texture2D BuildFieldTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "BioPortalGateFieldTex", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave
            };
            var px = new Color[size * size];
            const float cells = 6f;                       // số ô theo mỗi chiều (chẵn để ảnh lặp khít)
            Color edge = new Color(0.13f, 0.83f, 0.93f);
            Color core = new Color(0.06f, 0.72f, 0.51f);
            float k = 1.7320508f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // lưới lục giác: khoảng cách tới tâm ô gần nhất trong hai lưới so le
                    float u = x / (float)size * cells, v = y / (float)size * cells / k * 2f;
                    Vector2 p = new Vector2(u, v * k * 0.5f);
                    Vector2 a = new Vector2(Mathf.Repeat(p.x, 1f) - 0.5f, Mathf.Repeat(p.y, k) - k * 0.5f);
                    Vector2 b = new Vector2(Mathf.Repeat(p.x + 0.5f, 1f) - 0.5f, Mathf.Repeat(p.y + k * 0.5f, k) - k * 0.5f);
                    Vector2 g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
                    g = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
                    float d = Mathf.Max(g.x, g.x * 0.5f + g.y * (k * 0.5f));   // 0 ở tâm ô, 0,5 ở viền
                    float line = Mathf.SmoothStep(0.42f, 0.5f, d);
                    float fill = (1f - d * 2f) * 0.10f;
                    Color c = edge * line * 0.85f + core * Mathf.Max(0f, fill);
                    c.a = Mathf.Clamp01(Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        void UpdateGateVisuals()
        {
            if (gateFieldMat == null) return;
            // màng sáng dần khi thẻ mời hiện (người dùng ở gần), trôi chậm lên trên
            fieldLevel = Mathf.MoveTowards(fieldLevel, isCardShown ? 0.95f : 0.38f, Time.deltaTime * 1.6f);
            float pulse = 0.92f + 0.08f * Mathf.Sin(Time.time * 2.1f);
            float lv = fieldLevel * pulse;
            var tint = new Color(lv, lv, lv, lv);
            gateFieldMat.SetColor(baseColorPropId, tint);
            gateFieldMat.SetColor(colorPropId, tint);
            var off = new Vector2(0f, Mathf.Repeat(Time.time * 0.05f, 1f));
            gateFieldMat.SetTextureOffset("_BaseMap", off);
            if (gateGlowMat != null)
            {
                float g = isCardShown ? 1.25f : 0.8f + 0.2f * Mathf.Sin(Time.time * (Mathf.PI * 0.5f));
                gateGlowMat.SetColor(baseColorPropId, new Color(0.22f * g, 0.85f * g, 1.1f * g));
            }
        }

        /// <summary>Người dùng bước vào lòng cổng: tự vào phòng. Phải lùi ra xa 1,3 m thì lần bước qua kế tiếp mới tính.</summary>
        void CheckWalkThrough(Vector3 eyePos)
        {
            if (!walkThroughEnters || gateRoot == null) return;
            Vector3 local = transform.InverseTransformPoint(eyePos);
            float flat = new Vector2(local.x, local.z).magnitude;
            if (flat > 1.3f) { walkArmed = true; return; }
            if (!walkArmed) return;
            if (Mathf.Abs(local.x) < gateWidth * 0.5f - 0.1f && Mathf.Abs(local.z) < 0.30f)
            {
                walkArmed = false;
                OnEnterButtonClicked();
            }
        }

        void DestroyGate()
        {
            if (gateFieldTex != null) Destroy(gateFieldTex);
            if (gateFieldMat != null) Destroy(gateFieldMat);
            if (gateFrameMat != null) Destroy(gateFrameMat);
            if (gateGlowMat != null) Destroy(gateGlowMat);
        }
    }
}
