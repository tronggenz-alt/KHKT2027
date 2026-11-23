using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.Menu
{
    /// <summary>
    /// Chụp ảnh dựng thật của phòng thí nghiệm trong PlayMode (sau khi lab dựng xong, ẩn mọi giao diện) để làm nền màn chính
    /// và ảnh thu nhỏ (C4). Gọi từ Editor/KhktUiVerify.ChupLab; ảnh ghi vào KHKT_RA (mặc định Path.GetTempPath()/vera_ui_lab).
    /// </summary>
    public class KhktChupLabRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            string ra = System.Environment.GetEnvironmentVariable("KHKT_RA");
            if (string.IsNullOrEmpty(ra)) ra = Path.Combine(Path.GetTempPath(), "vera_ui_lab");
            Directory.CreateDirectory(ra);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 12f) yield return null;      // chờ lab dựng xong và ánh sáng ổn định
            foreach (var c in FindObjectsByType<Canvas>()) if (c.isRootCanvas) c.enabled = false;
            yield return null;
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[UI_FATAL] Không có camera chính"); Thoat(1); yield break; }
            var tr = cam.transform;
            var (p0, r0, f0) = (tr.position, tr.rotation, cam.fieldOfView);
            int n = 0;
            foreach (var (ten, lui, nang, cui, xoay, fov) in new[] { ("lab_rong", 1.2f, 0.25f, 6f, 0f, 60f), ("lab_gan", 0f, 0f, 12f, 0f, 48f), ("lab_phai", 0.6f, 0.1f, 8f, 35f, 55f) })
            {
                tr.position = p0 - tr.forward * lui + Vector3.up * nang;
                tr.rotation = Quaternion.Euler(r0.eulerAngles.x + cui, r0.eulerAngles.y + xoay, 0f);
                cam.fieldOfView = fov;
                yield return null;
                var rt = new RenderTexture(2880, 1620, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                var cu = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(2880, 1620, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 2880, 1620), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = cu;
                File.WriteAllBytes(Path.Combine(ra, ten + ".png"), tex.EncodeToPNG());
                rt.Release();
                n++;
                tr.SetPositionAndRotation(p0, r0);
                cam.fieldOfView = f0;
            }
            Debug.Log($"[UI_LAB] {n} ảnh (PlayMode) → {ra}");
            Thoat(0);
        }

        static void Thoat(int ma)
        {
#if UNITY_EDITOR
            EditorApplication.Exit(ma);
#endif
        }
    }
}
