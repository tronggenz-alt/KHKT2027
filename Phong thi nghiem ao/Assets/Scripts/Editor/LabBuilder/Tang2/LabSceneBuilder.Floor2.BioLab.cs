using System.Collections.Generic;
using System.IO;
using Khkt.Demo.Bio;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        public static void BuildFloor2BioLab(GameObject parent)
        {
            var lab = new GameObject("Phòng thí nghiệm sinh học");
            lab.transform.SetParent(parent.transform, false);

            // Import hoặc lấy BodyParts3D meshes
            var bp3dMeshes = Bp3dImporter.ImportBodyParts3D();

            // 1. KHU GIÁO VIÊN & BẢNG (Tường Tây x = 0..3)
            BuildBioTeacherArea(lab);

            // 2. 9 BÀN BIOBENCH HỌC SINH ({5.2, 8.6, 12.0} x {2.6, 7.5, 12.4})
            float[] xs = { 5.2f, 8.6f, 12.0f };
            float[] zs = { 2.6f, 7.5f, 12.4f };

            int benchIndex = 1;
            for (int ix = 0; ix < xs.Length; ix++)
            {
                for (int iz = 0; iz < zs.Length; iz++)
                {
                    BuildBioBench(lab, $"Ban_HocSinh_{benchIndex++}", new Vector3(xs[ix], 0, zs[iz]));
                }
            }

            // 3. KHU GIẢI PHẪU 3D (x 17.2..19.9, z 9..13.5)
            BuildAnatomyArea(lab, bp3dMeshes);

            // 4. KHU TỦ ẤM & NỒI HẤP (x 17.2..19.9, z 3..7.5)
            BuildIncubatorAutoclaveArea(lab);

            // 5. 2 TỦ AN TOÀN SINH HỌC CẤP II (Sát tường bắc, x = 6.6 và x = 11.6, z = 14.3)
            BuildBiosafetyCabinet(lab, "TuAnToanSinhHoc_1", new Vector3(6.6f, 0, 14.3f));
            BuildBiosafetyCabinet(lab, "TuAnToanSinhHoc_2", new Vector3(11.6f, 0, 14.3f));

            // 6. KHU AN TOÀN & RỬA KHẨN CẤP (Góc Tây Nam x = 1.0, z = 1.2)
            BuildBioSafetyStation(lab, new Vector3(1.0f, 0, 1.2f));

            // 7. ÁP PHÍCH & BIỂN BÁO PHÒNG SINH
            BuildBioPosters(lab);
        }

        static void BuildBioTeacherArea(GameObject parent)
        {
            var grp = new GameObject("KhuGiaoVien");
            grp.transform.SetParent(parent.transform, false);

            // Bảng từ xanh viết phấn / bút lông (4.0m x 1.3m ở tường tây x = 0.05, z = 7.5, y = 1.8)
            var board = LabKit.Box(grp, "BangTu", new Vector3(0.06f, 1.8f, 7.5f), new Vector3(0.04f, 1.3f, 4.2f), LabKit.Mat("BangTuXanh", new Color(0.12f, 0.28f, 0.18f), smoothness: 0.15f));
            LabKit.Box(board, "KhungBang", new Vector3(0.06f, 1.8f, 7.5f), new Vector3(0.05f, 1.34f, 4.24f), LabKit.Mat("KhungNhom", new Color(0.8f, 0.82f, 0.85f), metallic: 0.8f, smoothness: 0.6f));
            LabKit.Label(board, "TieuDeBang", "PHÒNG THÍ NGHIỆM SINH HỌC - VERA LAB", new Vector3(0.09f, 2.3f, 7.5f), 0.14f, Color.white);

            // Bàn giáo viên (dài 2.2m, rộng 0.8m, cao 0.8m)
            var tBench = LabKit.RoundBox(grp, "BanGiaoVien", new Vector3(2.0f, 0.4f, 7.5f), new Vector3(0.85f, 0.8f, 2.2f), 0.006f, t2BenchQuocTe ?? LabKit.Mat("MatBanDen", new Color(0.15f, 0.16f, 0.18f), smoothness: 0.4f));
            // Kính hiển vi của giáo viên
            BuildMicroscope(tBench, "KinhHienVi_GiaoVien", new Vector3(1.9f, 0.8f, 7.5f));
        }

        public static void BuildBioBench(GameObject parent, string name, Vector3 pos)
        {
            var bench = new GameObject(name);
            bench.transform.SetParent(parent.transform, false);

            // Mặt bàn Phenolic chống hóa chất (2.6m x 0.9m, cao 0.8m) bo cạnh R 6mm
            var matBan = t2BenchQuocTe ?? LabKit.Mat("MatBanDen", new Color(0.15f, 0.16f, 0.18f), smoothness: 0.4f);
            var top = LabKit.RoundBox(bench, "MatBan", pos + new Vector3(0, 0.77f, 0), new Vector3(0.9f, 0.06f, 2.6f), 0.006f, matBan);

            // Khung chân thép hộp sơn tĩnh điện (Than chì)
            var matKhung = t2Graphite ?? LabKit.Mat("ThepHopDen", new Color(0.18f, 0.19f, 0.2f), metallic: 0.3f, smoothness: 0.4f);
            LabKit.Box(bench, "Chan_1", pos + new Vector3(-0.38f, 0.37f, -1.2f), new Vector3(0.06f, 0.74f, 0.06f), matKhung);
            LabKit.Box(bench, "Chan_2", pos + new Vector3(0.38f, 0.37f, -1.2f), new Vector3(0.06f, 0.74f, 0.06f), matKhung);
            LabKit.Box(bench, "Chan_3", pos + new Vector3(-0.38f, 0.37f, 1.2f), new Vector3(0.06f, 0.74f, 0.06f), matKhung);
            LabKit.Box(bench, "Chan_4", pos + new Vector3(0.38f, 0.37f, 1.2f), new Vector3(0.06f, 0.74f, 0.06f), matKhung);

            // Thanh giằng ngang
            LabKit.Box(bench, "Giang_1", pos + new Vector3(0, 0.15f, -1.2f), new Vector3(0.76f, 0.04f, 0.04f), matKhung);
            LabKit.Box(bench, "Giang_2", pos + new Vector3(0, 0.15f, 1.2f), new Vector3(0.76f, 0.04f, 0.04f), matKhung);

            // Bồn rửa đầu bàn (polypropylene chống axit/kiềm) ở đầu phía tây
            var sinkMat = LabKit.Mat("BonRuaPP", new Color(0.25f, 0.27f, 0.28f), smoothness: 0.3f);
            LabKit.Box(bench, "BonRua", pos + new Vector3(-0.32f, 0.65f, 0), new Vector3(0.24f, 0.25f, 0.45f), sinkMat);
            // Vòi rửa cổ ngỗng inox
            var faucetMat = LabKit.Mat("Inox304", new Color(0.85f, 0.87f, 0.88f), metallic: 0.9f, smoothness: 0.85f);
            LabKit.Cyl(bench, "VoiCoNgong", pos + new Vector3(-0.4f, 0.95f, 0), 0.015f, 0.3f, faucetMat);

            // Trụ ổ điện an toàn chống nước giữa bàn
            LabKit.Box(bench, "O_Dien", pos + new Vector3(0, 0.9f, 0), new Vector3(0.12f, 0.18f, 0.25f), LabKit.Mat("HopODien", new Color(0.9f, 0.9f, 0.92f)));

            // 3 Kính hiển vi đặt dọc theo chiều dài bàn
            float[] microZs = { -0.8f, 0.0f, 0.8f };
            for (int m = 0; m < 3; m++)
            {
                BuildMicroscope(bench, $"KinhHienVi_{m + 1}", pos + new Vector3(0.18f, 0.8f, microZs[m]));
                // Ghế đẩu tương ứng
                BuildBioStool(bench, $"Ghe_{m + 1}", pos + new Vector3(0.8f, 0, microZs[m]));
            }

            // Hộp tiêu bản cố định (6 tiêu bản H&E)
            BuildSlideBox(bench, "HopTieuBan", pos + new Vector3(-0.15f, 0.81f, -0.6f));
        }

        public static void BuildMicroscope(GameObject parent, string name, Vector3 pos)
        {
            var mic = new GameObject(name);
            mic.transform.SetParent(parent.transform, false);

            var matBody = LabKit.Mat("KinhHienVi_Than", new Color(0.94f, 0.95f, 0.96f), smoothness: 0.6f);
            var matBlack = LabKit.Mat("KinhHienVi_Den", new Color(0.12f, 0.13f, 0.14f), smoothness: 0.5f);
            var matChrome = LabKit.Mat("KinhHienVi_Inox", new Color(0.85f, 0.86f, 0.88f), metallic: 0.9f, smoothness: 0.8f);

            // Đế kính hiển vi bo góc (R 3mm)
            LabKit.RoundBox(mic, "DeKinh", pos + new Vector3(0, 0.025f, 0), new Vector3(0.18f, 0.05f, 0.24f), 0.003f, matBody);

            // Đèn LED chiếu sáng ở đế (phát sáng trắng ấm)
            var ledMat = LabKit.Mat("KinhHienVi_LED", Color.white, smoothness: 0.9f, emission: new Color(1.0f, 0.95f, 0.85f) * 2.0f);
            LabKit.Cyl(mic, "DenChieu", pos + new Vector3(0, 0.055f, 0.04f), 0.025f, 0.015f, ledMat);

            // Thân cong nghiêng về phía sau
            LabKit.Box(mic, "ThanKinh", pos + new Vector3(0, 0.20f, -0.06f), new Vector3(0.06f, 0.32f, 0.08f), matBody);

            // Bàn soi mẫu (Stage) màu đen với 2 kẹp lam
            var stage = LabKit.Box(mic, "BanSoiMau", pos + new Vector3(0, 0.16f, 0.04f), new Vector3(0.14f, 0.015f, 0.13f), matBlack);
            // Lỗ xuyên sáng ở giữa bàn soi
            LabKit.Cyl(stage, "LoXuyenSang", pos + new Vector3(0, 0.168f, 0.04f), 0.012f, 0.002f, ledMat);

            // Mâm xoay vật kính (Revolving Nosepiece)
            var nosepiece = LabKit.Cyl(mic, "MamXoayVatKinh", pos + new Vector3(0, 0.25f, 0.04f), 0.04f, 0.018f, matBlack);

            // 4 Vật kính với 4 vòng màu chuẩn quốc tế:
            // 4x: Đỏ, 10x: Vàng, 40x: Xanh nhạt, 100x: Trắng
            var matRed = LabKit.Mat("VongMau_4x", new Color(0.9f, 0.15f, 0.15f));
            var matYellow = LabKit.Mat("VongMau_10x", new Color(0.95f, 0.82f, 0.1f));
            var matCyan = LabKit.Mat("VongMau_40x", new Color(0.15f, 0.65f, 0.95f));
            var matWhite = LabKit.Mat("VongMau_100x", Color.white);

            BuildObjectiveLens(nosepiece, "VatKinh_4x", new Vector3(0, -0.025f, 0.022f), matRed, 0.035f);
            BuildObjectiveLens(nosepiece, "VatKinh_10x", new Vector3(0.022f, -0.025f, 0), matYellow, 0.042f);
            BuildObjectiveLens(nosepiece, "VatKinh_40x", new Vector3(0, -0.025f, -0.022f), matCyan, 0.048f);
            BuildObjectiveLens(nosepiece, "VatKinh_100x", new Vector3(-0.022f, -0.025f, 0), matWhite, 0.052f);

            // Đầu quan sát hai mắt (Binocular tube) nghiêng 30°
            var head = new GameObject("DauQuanSat");
            head.transform.SetParent(mic.transform, false);
            head.transform.localPosition = pos + new Vector3(0, 0.35f, -0.02f);
            head.transform.localRotation = Quaternion.Euler(-30f, 0, 0);

            LabKit.Box(head, "KhoiLangKinh", Vector3.zero, new Vector3(0.08f, 0.06f, 0.08f), matBody);
            // 2 Ống thị kính
            LabKit.Cyl(head, "ThiKinh_Trai", new Vector3(-0.032f, 0.06f, 0.03f), 0.015f, 0.07f, matBlack);
            LabKit.Cyl(head, "ThiKinh_Phai", new Vector3(0.032f, 0.06f, 0.03f), 0.015f, 0.07f, matBlack);

            // Núm chỉnh thô và chỉnh tinh đồng trục hai bên
            LabKit.Cyl(mic, "NumChinhTho_T", pos + new Vector3(-0.042f, 0.12f, -0.06f), 0.025f, 0.02f, matBlack);
            LabKit.Cyl(mic, "NumChinhTinh_T", pos + new Vector3(-0.054f, 0.12f, -0.06f), 0.015f, 0.015f, matChrome);
            LabKit.Cyl(mic, "NumChinhTho_P", pos + new Vector3(0.042f, 0.12f, -0.06f), 0.025f, 0.02f, matBlack);
            LabKit.Cyl(mic, "NumChinhTinh_P", pos + new Vector3(0.054f, 0.12f, -0.06f), 0.015f, 0.015f, matChrome);

            // Gắn MicroscopeController điều khiển tương tác
            var ctrl = mic.AddComponent<MicroscopeController>();
            ctrl.nosepiece = nosepiece.transform;
        }

        static void BuildObjectiveLens(GameObject parent, string name, Vector3 localPos, Material ringMat, float length)
        {
            var matMetal = LabKit.Mat("KinhHienVi_Inox", new Color(0.85f, 0.86f, 0.88f), metallic: 0.9f, smoothness: 0.8f);
            var lens = LabKit.Cyl(parent, name, localPos, 0.011f, length, matMetal);
            // Vòng màu nhận diện chuẩn
            LabKit.Cyl(lens, "VongMau", localPos + new Vector3(0, length * 0.25f, 0), 0.0115f, 0.005f, ringMat);
        }

        static void BuildBioStool(GameObject parent, string name, Vector3 pos)
        {
            var stool = new GameObject(name);
            stool.transform.SetParent(parent.transform, false);

            var matSeat = t2OakWood ?? LabKit.Mat("MatGheGo", new Color(0.8f, 0.72f, 0.6f), smoothness: 0.35f);
            var matLegs = t2Graphite ?? LabKit.Mat("ThepHopDen", new Color(0.18f, 0.19f, 0.2f), metallic: 0.4f, smoothness: 0.5f);

            // Mặt ghế tròn xoay (đường kính 32 cm, dày 3 cm)
            LabKit.Cyl(stool, "MatGhe", pos + new Vector3(0, 0.5f, 0), 0.16f, 0.03f, matSeat);
            // Trục giữa & 4 chân thép xòe
            LabKit.Cyl(stool, "TrucGhe", pos + new Vector3(0, 0.25f, 0), 0.025f, 0.45f, matLegs);
            LabKit.Cyl(stool, "VongGacChan", pos + new Vector3(0, 0.15f, 0), 0.18f, 0.015f, matLegs);
        }

        static void BuildSlideBox(GameObject parent, string name, Vector3 pos)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent.transform, false);

            var matBox = LabKit.Mat("HopLamKinh", new Color(0.92f, 0.88f, 0.82f), smoothness: 0.3f);
            LabKit.RoundBox(box, "HopGong", pos, new Vector3(0.14f, 0.035f, 0.20f), 0.004f, matBox);
            LabKit.Label(box, "NhanHop", "HỘP TIÊU BẢN CỐ ĐỊNH (H&E)", pos + new Vector3(0, 0.019f, 0), 0.014f, Color.black);
        }

        static void BuildAnatomyArea(GameObject parent, Dictionary<string, Mesh> meshes)
        {
            var grp = new GameObject("KhuGiaiPhau");
            grp.transform.SetParent(parent.transform, false);

            // Bàn giải phẫu số (Digital Anatomy Table) tại x = 18.5, z = 11.25
            Vector3 tablePos = new Vector3(18.5f, 0, 11.25f);
            var table = new GameObject("BanGiaiPhauSo");
            table.transform.SetParent(grp.transform, false);

            // Khung bàn giải phẫu y tế (2.2m x 0.8m x cao 0.85m)
            var matTableBody = LabKit.Mat("BanGiaiPhau_Than", new Color(0.95f, 0.96f, 0.97f), smoothness: 0.6f);
            LabKit.RoundBox(table, "ThanBan", tablePos + new Vector3(0, 0.425f, 0), new Vector3(0.85f, 0.85f, 2.2f), 0.008f, matTableBody);

            // Mặt màn hình nằm 2.1m x 0.65m phát sáng
            var screenMat = LabKit.Mat("ManHinhGiaiPhau", Color.black, smoothness: 0.95f, emission: new Color(0.08f, 0.18f, 0.28f) * 1.5f);
            LabKit.Box(table, "ManHinhNam", tablePos + new Vector3(0, 0.855f, 0), new Vector3(0.68f, 0.01f, 2.1f), screenMat);

            // Nhãn bản quyền CC BY 4.0 DBCLS
            LabKit.Label(table, "NhanBanQuyen", "BodyParts3D © DBCLS (CC BY 4.0)", tablePos + new Vector3(0.35f, 0.86f, -0.9f), 0.02f, new Color(0.6f, 0.8f, 1.0f));

            // Đặt các lớp mô hình BodyParts3D nằm trên mặt bàn (tỷ lệ 1/3 để vừa bàn)
            var modelRoot = new GameObject("MoHinhNguoi");
            modelRoot.transform.SetParent(table.transform, false);
            // Xoay nằm ngửa trên bàn: đầu hướng bắc (+z)
            modelRoot.transform.localPosition = tablePos + new Vector3(0, 0.92f, 0);
            modelRoot.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            modelRoot.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var anat = table.AddComponent<DigitalAnatomyTable>();

            if (meshes.ContainsKey("xuong"))
            {
                var mf = modelRoot.AddComponent<MeshFilter>();
                mf.sharedMesh = meshes["xuong"];
                var mr = modelRoot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = LabKit.Mat("BP3D_Xuong", new Color(0.92f, 0.89f, 0.82f), smoothness: 0.4f);
                anat.layerSkeleton = modelRoot;
            }

            // Bộ xương đứng 1.7m trên giá bánh xe bên cạnh bàn (x = 19.5, z = 12.8)
            BuildStandingSkeleton(grp, new Vector3(19.5f, 0, 12.8f), meshes.ContainsKey("xuong") ? meshes["xuong"] : null);
        }

        static void BuildStandingSkeleton(GameObject parent, Vector3 pos, Mesh skeletonMesh)
        {
            var skel = new GameObject("BoXuongDung");
            skel.transform.SetParent(parent.transform, false);

            var matStand = LabKit.Mat("GiaDoInox", new Color(0.82f, 0.84f, 0.86f), metallic: 0.85f, smoothness: 0.75f);
            // Đế 5 bánh xe
            LabKit.Cyl(skel, "DeBanhXe", pos + new Vector3(0, 0.05f, 0), 0.28f, 0.04f, matStand);
            // Cột inox đứng cao 1.85m
            LabKit.Cyl(skel, "CotInox", pos + new Vector3(0, 0.95f, -0.1f), 0.018f, 1.8f, matStand);
            // Móc treo trên đầu
            LabKit.Box(skel, "MocTreo", pos + new Vector3(0, 1.82f, -0.05f), new Vector3(0.02f, 0.04f, 0.12f), matStand);

            // Khối bộ xương thật cao 1.7m
            var bones = new GameObject("Xuong");
            bones.transform.SetParent(skel.transform, false);
            bones.transform.localPosition = pos + new Vector3(0, 0.95f, 0);
            bones.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

            var mf = bones.AddComponent<MeshFilter>();
            mf.sharedMesh = skeletonMesh != null ? skeletonMesh : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var mr = bones.AddComponent<MeshRenderer>();
            mr.sharedMaterial = LabKit.Mat("BP3D_Xuong", new Color(0.92f, 0.89f, 0.82f), smoothness: 0.4f);
        }

        static void BuildIncubatorAutoclaveArea(GameObject parent)
        {
            var grp = new GameObject("KhuTuAmNoiHap");
            grp.transform.SetParent(parent.transform, false);

            // Bàn kệ bê tông / phenolic chịu nhiệt (x = 18.5, z = 5.25)
            Vector3 counterPos = new Vector3(18.5f, 0, 5.25f);
            var counterMat = LabKit.Mat("MatBanInox", new Color(0.85f, 0.87f, 0.88f), metallic: 0.8f, smoothness: 0.6f);
            LabKit.RoundBox(grp, "KeDatMay", counterPos + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 3.8f), 0.008f, counterMat);

            // 1. Tủ ấm vi sinh 37.0°C (Microbiological Incubator)
            Vector3 incPos = counterPos + new Vector3(0, 0.8f, -1.0f);
            var matInc = LabKit.Mat("TuAm_Vo", new Color(0.93f, 0.94f, 0.95f), smoothness: 0.5f);
            var inc = LabKit.RoundBox(grp, "TuAmViSinh", incPos + new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.7f, 0.65f), 0.005f, matInc);
            // Màn hình LED 37.0°C
            var ledMat = LabKit.Mat("LED_XanhLuc", Color.black, emission: new Color(0.1f, 1.0f, 0.2f) * 2.5f);
            LabKit.Box(inc, "ManHinhNhietDo", incPos + new Vector3(-0.305f, 0.55f, 0.15f), new Vector3(0.01f, 0.06f, 0.16f), ledMat);
            LabKit.Label(inc, "SoNhietDo", "37.0 °C", incPos + new Vector3(-0.315f, 0.55f, 0.15f), 0.025f, Color.green);

            // 2. Nồi hấp tiệt trùng đứng (Vertical Autoclave)
            Vector3 autoPos = counterPos + new Vector3(0, 0.8f, 1.0f);
            var matSteel = LabKit.Mat("NoiHap_Inox", new Color(0.88f, 0.90f, 0.92f), metallic: 0.9f, smoothness: 0.8f);
            var autoclave = LabKit.Cyl(grp, "NoiHapTietTrung", autoPos + new Vector3(0, 0.4f, 0), 0.22f, 0.8f, matSteel);
            // Nắp vô lăng xoay 6 chấu
            LabKit.Cyl(autoclave, "NapVoLang", autoPos + new Vector3(0, 0.83f, 0), 0.16f, 0.04f, matSteel);
            // Đồng hồ đo áp suất kim
            var matDial = LabKit.Mat("DongHoApSuat", Color.white, smoothness: 0.7f);
            LabKit.Cyl(autoclave, "DongHoApSuat", autoPos + new Vector3(-0.16f, 0.72f, 0), 0.04f, 0.02f, matDial);
        }

        static void BuildBiosafetyCabinet(GameObject parent, string name, Vector3 pos)
        {
            var bsc = new GameObject(name);
            bsc.transform.SetParent(parent.transform, false);

            var matShell = LabKit.Mat("BSC_Vo", new Color(0.95f, 0.96f, 0.97f), smoothness: 0.55f);
            var matGlass = LabKit.Mat("BSC_Kinh", new Color(0.8f, 0.92f, 0.95f, 0.45f), smoothness: 0.95f);
            var matGrille = LabKit.Mat("BSC_LuoiHut", new Color(0.2f, 0.22f, 0.24f), smoothness: 0.3f);

            // Thân tủ (rộng 1.3m, sâu 0.8m, cao 1.6m trên chân đỡ 0.7m)
            LabKit.RoundBox(bsc, "ThanTu", pos + new Vector3(0, 1.5f, 0), new Vector3(1.3f, 1.5f, 0.8f), 0.006f, matShell);
            // Chân đỡ thép hộp
            var matLegs = LabKit.Mat("BSC_Chan", new Color(0.25f, 0.27f, 0.29f), metallic: 0.4f, smoothness: 0.5f);
            LabKit.Box(bsc, "ChanDo", pos + new Vector3(0, 0.37f, 0), new Vector3(1.26f, 0.74f, 0.76f), matLegs);

            // Kính trượt nghiêng 10° phía trước
            var sash = new GameObject("KinhTruot");
            sash.transform.SetParent(bsc.transform, false);
            sash.transform.localPosition = pos + new Vector3(0, 1.35f, -0.38f);
            sash.transform.localRotation = Quaternion.Euler(10f, 0, 0);
            LabKit.Box(sash, "Kinh", Vector3.zero, new Vector3(1.18f, 0.65f, 0.01f), matGlass);

            // Lưới hút khí phía trước
            LabKit.Box(bsc, "LuoiHutKhi", pos + new Vector3(0, 0.78f, -0.35f), new Vector3(1.18f, 0.04f, 0.08f), matGrille);

            // Bảng điều khiển LED tốc độ gió (0.45 m/s)
            var matScreen = LabKit.Mat("BSC_ManHinh", Color.black, emission: new Color(0.1f, 0.8f, 0.95f) * 1.8f);
            LabKit.Box(bsc, "ManHinhTocDo", pos + new Vector3(0.5f, 1.95f, -0.405f), new Vector3(0.18f, 0.08f, 0.01f), matScreen);
            LabKit.Label(bsc, "SoTocDo", "0.45 m/s\nLAMINAR", pos + new Vector3(0.5f, 1.95f, -0.415f), 0.02f, Color.cyan);
        }

        static void BuildBioSafetyStation(GameObject parent, Vector3 pos)
        {
            var st = new GameObject("TramRuaKhanCap");
            st.transform.SetParent(parent.transform, false);

            var matYellow = LabKit.Mat("AnToan_Vang", new Color(0.96f, 0.78f, 0.1f), smoothness: 0.6f);
            var matGreen = LabKit.Mat("AnToan_Xanh", new Color(0.1f, 0.55f, 0.25f), smoothness: 0.6f);

            // Ống cấp nước màu vàng ANSI Z358.1
            LabKit.Cyl(st, "CotNuoc", pos + new Vector3(0, 1.1f, 0), 0.025f, 2.2f, matYellow);
            // Bát rửa mắt bằng thép không gỉ / composite xanh
            LabKit.Cyl(st, "BatRuaMat", pos + new Vector3(0, 1.0f, 0.2f), 0.16f, 0.08f, matGreen);
            // Vòi sen tắm khẩn cấp trên đỉnh (cao 2.2m)
            LabKit.Cyl(st, "VoiSenTam", pos + new Vector3(0, 2.2f, 0.35f), 0.12f, 0.04f, matGreen);
        }

        static void BuildBioPosters(GameObject parent)
        {
            var p = new GameObject("ApPhichVaBienBao");
            p.transform.SetParent(parent.transform, false);

            var matFrame = LabKit.Mat("KhungTranhNhom", new Color(0.2f, 0.2f, 0.2f), smoothness: 0.5f);
            var matBiohazard = LabKit.Mat("BienBiohazard", new Color(0.95f, 0.45f, 0.1f));

            // Biển cảnh báo an toàn sinh học (Biohazard) ở cửa vào
            var bioSign = LabKit.Box(p, "BienNguyHaiSinhHoc", new Vector3(2.4f, 2.1f, 0.06f), new Vector3(0.45f, 0.35f, 0.02f), matBiohazard);
            LabKit.Label(bioSign, "ChuBiohazard", "BIOHAZARD\nCẤP ĐỘ AN TOÀN SINH HỌC 1 & 2", new Vector3(2.4f, 2.1f, 0.04f), 0.025f, Color.black);
        }
    }
}
