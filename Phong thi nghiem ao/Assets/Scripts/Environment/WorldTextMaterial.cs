using UnityEngine;

namespace Khkt.Environment
{
    /// <summary>
    /// Gắn ảnh phông chữ động vào vật liệu chữ dùng chung (shader Khkt/ChuTrongCanh) của mọi biển chữ.
    /// Phông động tạo ảnh lúc chạy, nên phải gán lại mỗi khi mở cảnh và mỗi khi phông vẽ thêm ký tự.
    /// </summary>
    [ExecuteAlways]
    public class WorldTextMaterial : MonoBehaviour
    {
        public Material textMaterial;
        public Font font;

        void OnEnable()
        {
            Apply();
            Font.textureRebuilt += OnTextureRebuilt;
        }

        void OnDisable() => Font.textureRebuilt -= OnTextureRebuilt;

        void OnTextureRebuilt(Font f)
        {
            if (f == font) Apply();
        }

        void Apply()
        {
            if (textMaterial != null && font != null) textMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
