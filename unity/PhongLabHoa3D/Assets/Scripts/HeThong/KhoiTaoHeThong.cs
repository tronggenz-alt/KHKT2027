using System;
using System.IO;
using UnityEngine;
using PhongThiNghiem.GiaoDien;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.ToaNha;

namespace PhongThiNghiem.HeThong
{
    /// <summary>
    /// Tự động khởi tạo toàn bộ hệ thống phòng lab khi scene load:
    /// Dựng phòng, dựng 5 trạm, nhân vật giáo sư, camera, giao diện và kiểm tra đối số dòng lệnh.
    /// </summary>
    public static class KhoiTaoHeThong
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void KhoiDong()
        {
            Debug.Log("[KhoiTaoHeThong] Bắt đầu khởi tạo hệ thống Phòng Lab Hoá 3D...");

            // 1. Ánh sáng Directional Light
            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var goLight = new GameObject("AnhSangMatTroi");
                var light = goLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1.0f, 0.98f, 0.95f);
                light.intensity = 1.2f;
                goLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 2. Camera chính
            Camera cam = Camera.main;
            if (cam == null)
            {
                var goCam = new GameObject("MainCamera");
                cam = goCam.AddComponent<Camera>();
                goCam.tag = "MainCamera";
                goCam.AddComponent<AudioListener>();
            }

            // 3. Quản lý toà nhà, kho mô hình 3D & hệ thống âm thanh, giao diện
            if (DungToaNha.Instance == null && DungPhongLab.Instance == null)
            {
                var goPhong = new GameObject("PhongLabManager");
                goPhong.AddComponent<AmThanhLab>();
                goPhong.AddComponent<KhoMoHinh>();
                goPhong.AddComponent<DungToaNha>();
                goPhong.AddComponent<QuanLyTram>();
                goPhong.AddComponent<GiaoDienApp>();
            }

            // 4. Dựng nhân vật giáo sư (DieuKhienNhanVat + DungNhanVat)
            if (DieuKhienNhanVat.Instance == null)
            {
                var goNV = new GameObject("GiaoSuBlouseTrang");
                goNV.transform.position = new Vector3(18.0f, 0.1f, 27.0f); // Sảnh đón tiếp toà nhà 36x30m
                goNV.AddComponent<DungNhanVat>();
                var dk = goNV.AddComponent<DieuKhienNhanVat>();
                dk.CameraChinh = cam;
            }

            // 5. Kiểm tra đối số dòng lệnh -tu-kiem-tra
            KiemTraDoiSoTuKiemTra();
        }

        private static void KiemTraDoiSoTuKiemTra()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-tu-kiem-tra" && i + 1 < args.Length)
                {
                    string thuMucXuat = args[i + 1];
                    Debug.Log($"[KhoiTaoHeThong] Phát hiện cờ -tu-kiem-tra: {thuMucXuat}");

                    var goKiemTra = new GameObject("TuKiemTraRunner");
                    var runner = goKiemTra.AddComponent<TuKiemTraApp>();
                    runner.BatDauTuKiemTra(thuMucXuat);
                    break;
                }
            }
        }
    }
}
