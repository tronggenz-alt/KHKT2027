using System;
using UnityEngine;

namespace PhongThiNghiem.HeThong
{
    /// <summary>
    /// Quản lý âm thanh 3D trong phòng thí nghiệm:
    /// - Tiếng bước chân trên sàn vinyl / gạch
    /// - Tiếng nhỏ giọt / rót chất lỏng
    /// - Tiếng bọt khí sủi trong điện phân / phản ứng
    /// - Tiếng click công tắc điện và van khoá
    /// - Tiếng chuông chúc mừng hoàn thành thí nghiệm
    /// Hỗ trợ tổng hợp âm thanh thủ tục (Procedural Synth) chạy offline không cần tải file ngoài!
    /// </summary>
    [ExecuteAlways]
    public class AmThanhLab : MonoBehaviour
    {
        private static AmThanhLab _instance;
        public static AmThanhLab Instance
        {
            get
            {
                if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<AmThanhLab>();
                return _instance;
            }
            private set { _instance = value; }
        }

        [Range(0f, 1f)] public float AmLuongChung = 0.8f;
        public bool BatAmThanh = true;

        private AudioSource _audioNguon3D;
        private AudioClip _clipBuocChan;
        private AudioClip _clipRotNuoc;
        private AudioClip _clipBotSui;
        private AudioClip _clipCongTac;
        private AudioClip _clipThanhCong;

        void OnEnable()
        {
            _instance = this;
            if (_audioNguon3D == null)
            {
                _audioNguon3D = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                _audioNguon3D.spatialBlend = 0.85f;
            }
            if (_clipBuocChan == null) KhoiTaoAudioClipsThuTuc();
        }

        void Awake()
        {
            _instance = this;
            _audioNguon3D = gameObject.AddComponent<AudioSource>();
            _audioNguon3D.spatialBlend = 0.85f; // Âm thanh 3D
            _audioNguon3D.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioNguon3D.minDistance = 1.0f;
            _audioNguon3D.maxDistance = 18.0f;

            KhoiTaoAudioClipsThuTuc();
        }

        private void KhoiTaoAudioClipsThuTuc()
        {
            // 1. Tiếng bước chân (Footstep: tiếng đập nhẹ tần số thấp 120Hz)
            _clipBuocChan = TaoClip("BuocChan", 0.08f, (t, s) => {
                float env = Mathf.Exp(-t * 40f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.3f;
                float tone = Mathf.Sin(2f * Mathf.PI * 120f * t);
                return (tone * 0.7f + noise) * env;
            });

            // 2. Tiếng giọt nước rót (Droplet / Pouring ping: 850Hz trượt lên 1200Hz)
            _clipRotNuoc = TaoClip("RotNuoc", 0.12f, (t, s) => {
                float env = Mathf.Exp(-t * 25f);
                float freq = Mathf.Lerp(850f, 1200f, t / 0.12f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            });

            // 3. Tiếng bọt sủi điện phân (Fizz / Bubbling: tiếng nổ bọt li ti)
            _clipBotSui = TaoClip("BotSui", 0.25f, (t, s) => {
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                float ping = Mathf.Sin(2f * Mathf.PI * UnityEngine.Random.Range(600f, 1400f) * t) * 0.6f;
                return (noise + ping) * 0.45f;
            });

            // 4. Tiếng click công tắc van / khoá buret (Switch click: 2400Hz cực ngắn)
            _clipCongTac = TaoClip("CongTac", 0.04f, (t, s) => {
                float env = Mathf.Exp(-t * 120f);
                return Mathf.Sin(2f * Mathf.PI * 2400f * t) * env;
            });

            // 5. Tiếng chuông thành công (Major triad chord: C6, E6, G6)
            _clipThanhCong = TaoClip("ThanhCong", 0.6f, (t, s) => {
                float env = Mathf.Exp(-t * 5f);
                float c = Mathf.Sin(2f * Mathf.PI * 1046.5f * t);
                float e = Mathf.Sin(2f * Mathf.PI * 1318.5f * t);
                float g = Mathf.Sin(2f * Mathf.PI * 1567.9f * t);
                return ((c + e + g) / 3f) * env;
            });
        }

        private AudioClip TaoClip(string ten, float thoiLuongGiay, Func<float, int, float> hamSong)
        {
            int tanSoMau = 44100;
            int soMau = Mathf.CeilToInt(thoiLuongGiay * tanSoMau);
            float[] duLieu = new float[soMau];

            for (int i = 0; i < soMau; i++)
            {
                float t = (float)i / tanSoMau;
                duLieu[i] = Mathf.Clamp(hamSong(t, i), -1f, 1f);
            }

            var clip = AudioClip.Create(ten, soMau, 1, tanSoMau, false);
            clip.SetData(duLieu, 0);
            return clip;
        }

        public void PhatTiengBuocChan(Vector3 viTri)
        {
            PhatAm(_clipBuocChan, viTri, 0.45f);
        }

        public void PhatTiengRotNuoc(Vector3 viTri)
        {
            PhatAm(_clipRotNuoc, viTri, 0.65f);
        }

        public void PhatTiengBotSui(Vector3 viTri)
        {
            PhatAm(_clipBotSui, viTri, 0.5f);
        }

        public void PhatTiengCongTac(Vector3 viTri)
        {
            PhatAm(_clipCongTac, viTri, 0.7f);
        }

        public void PhatTiengThanhCong(Vector3 viTri)
        {
            PhatAm(_clipThanhCong, viTri, 0.85f);
        }

        private void PhatAm(AudioClip clip, Vector3 viTri, float tyLeAmLuong)
        {
            if (!BatAmThanh || clip == null) return;
            float vol = AmLuongChung * tyLeAmLuong;
            if (_audioNguon3D != null)
            {
                _audioNguon3D.transform.position = viTri;
                _audioNguon3D.PlayOneShot(clip, vol);
            }
        }
    }
}
