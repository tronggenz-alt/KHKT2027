using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem.ThiNghiem
{
    /// <summary>
    /// Hiệu ứng bọt khí sủi bọt (Điện phân, kim loại tan tạo khí) và kết tủa lắng đáy:
    /// - Quản lý các hạt bọt khí li ti bám trên điện cực rồi nổi lên bề mặt.
    /// - Mật độ bọt tỉ lệ với dòng điện I hoặc tốc độ phản ứng.
    /// - Quản lý hạt kết tủa lơ lửng và lắng dần xuống đáy.
    /// </summary>
    public class BotKhiThiNghiem : MonoBehaviour
    {
        public enum LoaiHieuUng
        {
            BotKhiDienPhan = 0,
            SuiBotKhiPhanUng = 1,
            KetTuaLang = 2
        }

        public LoaiHieuUng Loai = LoaiHieuUng.BotKhiDienPhan;
        public float CuongDo = 1.0f; // Tỉ lệ dòng điện I hoặc tốc độ
        public int SoLuongHat = 24;
        public float ChieuCaoCoc = 0.08f;
        public float BanKinhHat = 0.003f;
        public Color MauHat = new Color(0.9f, 0.95f, 1.0f, 0.65f);

        private readonly List<Transform> _cacHat = new List<Transform>();
        private readonly List<Vector3> _vanToc = new List<Vector3>();

        void Start()
        {
            KhoiTaoCacHat();
        }

        public void KhoiTaoCacHat()
        {
            // Xoá các hạt cũ nếu có
            foreach (var h in _cacHat)
            {
                if (h != null) DestroyImmediate(h.gameObject);
            }
            _cacHat.Clear();
            _vanToc.Clear();

            var mat = LabVatLieu.PhatSang(MauHat);

            for (int i = 0; i < SoLuongHat; i++)
            {
                var hat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hat.name = $"Hat_{Loai}_{i}";
                hat.transform.SetParent(transform, false);

                float rx = UnityEngine.Random.Range(-0.02f, 0.02f);
                float rz = UnityEngine.Random.Range(-0.02f, 0.02f);
                float ry = UnityEngine.Random.Range(0.005f, ChieuCaoCoc);

                hat.transform.localPosition = new Vector3(rx, ry, rz);
                hat.transform.localScale = Vector3.one * BanKinhHat;
                DestroyImmediate(hat.GetComponent<Collider>());
                hat.GetComponent<Renderer>().material = mat;

                _cacHat.Add(hat.transform);

                // Vận tốc hạt: bọt khí nổi lên (+Y), kết tủa chìm xuống (-Y)
                float vy = (Loai == LoaiHieuUng.KetTuaLang) ? -0.012f : 0.025f * Mathf.Max(0.2f, CuongDo);
                _vanToc.Add(new Vector3(UnityEngine.Random.Range(-0.002f, 0.002f), vy, UnityEngine.Random.Range(-0.002f, 0.002f)));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _cacHat.Count; i++)
            {
                var h = _cacHat[i];
                if (h == null) continue;

                Vector3 p = h.localPosition;
                Vector3 v = _vanToc[i];

                // Thêm dao động nhẹ ngang
                p.x += Mathf.Sin(Time.time * 4f + i) * 0.0005f;
                p.z += Mathf.Cos(Time.time * 4f + i) * 0.0005f;
                p.y += v.y * dt;

                if (Loai == LoaiHieuUng.KetTuaLang)
                {
                    // Kết tủa lắng xuống đáy
                    if (p.y <= 0.004f)
                    {
                        p.y = 0.004f; // Đọng dưới đáy
                    }
                }
                else
                {
                    // Bọt khí nổi lên mặt dung dịch rồi tái tạo ở đáy
                    if (p.y >= ChieuCaoCoc)
                    {
                        p.y = 0.005f;
                        p.x = UnityEngine.Random.Range(-0.02f, 0.02f);
                        p.z = UnityEngine.Random.Range(-0.02f, 0.02f);
                    }
                }

                h.localPosition = p;
            }
        }

        public void DatCuongDo(float i)
        {
            CuongDo = i;
            for (int idx = 0; idx < _vanToc.Count; idx++)
            {
                float vy = (Loai == LoaiHieuUng.KetTuaLang) ? -0.012f : 0.025f * Mathf.Max(0.2f, CuongDo);
                _vanToc[idx] = new Vector3(_vanToc[idx].x, vy, _vanToc[idx].z);
            }
        }
    }
}
