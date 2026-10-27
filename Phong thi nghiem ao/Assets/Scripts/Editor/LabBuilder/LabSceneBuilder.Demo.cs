using System.Collections.Generic;
using System.Linq;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Phần demo: bộ chuẩn độ chạy được (buret có vạch chia, cột dung dịch, mặt khum, khóa, giọt rơi,
    /// bình nón đổi màu), thẻ thông tin cho mọi dụng cụ và các thao tác bật/tắt (đèn cồn, vòi nước...).
    /// </summary>
    public static partial class LabSceneBuilder
    {
        const float BuretBaseY = 0.98f;     // đầu dưới buret khi lắp trên giá (độ cao tuyệt đối, bàn cao 0,8 m)
        const float ZeroMarkY = 0.70f;      // vạch 0 ml, tính từ đầu dưới buret
        const float MetersPerMl = 0.0105f;  // buret 50 ml: 50 vạch dài 52,5 cm
        const float ScaleBottom = 0.165f, ScaleTop = 0.71f;
        const float BuretTubeBottom = 0.07f;  // đáy ống buret, ngay trên khóa
        const float BuretTipMl = 0.9f;        // thể tích phần đầu buret dưới đáy ống
        const float BuretTubeTop = 0.755f;

        static BuretStopcock demoBuret;
        static LabContainer demoFlask, demoHcl, demoNaoh, demoWaste;
        static Pipette demoPipette;
        static PlaceSocket clampSocket, underSocket;
        static MeniscusReading demoReading;
        static SwirlDetector demoSwirl;
        static Camera viewerCamera;
        static TextMesh titrAnalyteLabel, titrTitrantLabel;
        static Dropper titrDropper;
        static Material ringWhite;

        // =====================================================================
        // Bộ chuẩn độ
        // =====================================================================

        /// <summary>
        /// Bộ dụng cụ chuẩn độ ở bàn 1, bày như lúc bắt đầu buổi thực hành: buret và pipet nằm trên giá đỡ,
        /// bình nón rỗng, cốc HCl, chai NaOH, lọ phenolphtalein, cốc thải, bình tia. Giá sắt có kẹp buret
        /// (chỗ đặt), gạch men trắng dưới đầu buret (chỗ đặt bình nón). Học sinh tự lắp, tự nạp, tự lấy.
        /// </summary>
        static void TitrationSet(Transform bench, int s)
        {
            const float top = 0.8f;
            var g = Group("Bộ chuẩn độ", bench);
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z * s);
            Quaternion lying = Quaternion.FromToRotation(Vector3.up, Vector3.forward * s);
            var naoh = Mat("DungDichNaOH", new Color(0.82f, 0.9f, 1f, 0.32f), 0.9f, 0, transparent: true);
            var ringMat = Mat("VongDat", new Color(0.4f, 0.9f, 1f, 0.5f), 0.3f, 0, emission: new Color(0.25f, 0.8f, 1.1f), transparent: true);
            ringWhite = Mat("VachTrang", new Color(0.97f, 0.97f, 0.97f), 0.4f, 0, emission: new Color(0.35f, 0.35f, 0.35f));

            // Giá sắt, kẹp (cọc lệch sang bên để người ngồi thấy cả giá lẫn buret)
            Box(g, "Đế giá sắt", L(0.02f, top + 0.006f, -0.03f), new Vector3(0.32f, 0.012f, 0.2f), darkMetal);
            // Kẹp ở trên vạch 0 để không che thang chia khi đọc
            const float clampY = 1.705f;
            Cyl(g, "Cọc giá sắt", L(-0.1f, top + 0.012f + 0.475f, -0.09f), 0.006f, 0.95f, steel);
            Box(g, "Tay kẹp", L(0.0f, clampY, -0.09f), new Vector3(0.2f, 0.014f, 0.014f), darkMetal, false);
            Box(g, "Cổ kẹp", L(0.1f, clampY, -0.045f), new Vector3(0.014f, 0.014f, 0.09f), darkMetal, false);
            Box(g, "Má kẹp", L(0.1f, clampY, 0.013f), new Vector3(0.025f, 0.03f, 0.008f), Mat("MaKep", new Color(0.65f, 0.15f, 0.1f), 0.3f), false);
            Box(g, "Khớp kẹp", L(-0.1f, clampY, -0.09f), new Vector3(0.03f, 0.04f, 0.03f), darkMetal, false);
            Box(g, "Gạch men trắng", L(0.1f, top + 0.016f, 0), new Vector3(0.15f, 0.008f, 0.15f), tileWhite);
            clampSocket = Socket(g, "Kẹp buret", L(0.1f, BuretBaseY, 0), new[] { "buret" }, true,
                                 new Vector3(0, 0.42f, 0), new Vector3(0.1f, 0.85f, 0.1f), new Vector3(0, clampY - BuretBaseY, 0), 0.03f, ringMat);
            underSocket = Socket(g, "Dưới buret", L(0.1f, top + 0.02f, 0), new[] { "binh_non", "coc" }, false,
                                 new Vector3(0, 0.08f, 0), new Vector3(0.17f, 0.16f, 0.17f), new Vector3(0, 0.002f, 0), 0.068f, ringMat);

            // Giá đỡ buret và pipet nằm ở mép bàn
            foreach (float z in new[] { -1.08f, -0.58f })
            {
                Box(g, "Giá đỡ nằm", L(0.36f, top + 0.004f, z), new Vector3(0.03f, 0.008f, 0.02f), darkMetal);
                Box(g, "Giá đỡ nằm", L(0.27f, top + 0.004f, z), new Vector3(0.03f, 0.008f, 0.02f), darkMetal);
            }

            // ---------- Buret 50 ml ----------
            var buret = Group("Buret", g, L(0.36f, top + 0.016f, -1.2f));
            buret.localRotation = lying;
            var buretMesh = Lathe("Buret", 16, V(0, 0), V(0.0012f, 0), V(0.0025f, 0.035f), V(0.0055f, 0.06f), V(0.0055f, 0.06f),
                                  V(0.0055f, 0.75f), V(0.0068f, 0.76f));
            NoShadow(MeshObj(buret, "Ống buret", buretMesh, Vector3.zero, labGlass, true));
            var tip = Child(buret, "Đầu buret", new Vector3(0, -0.012f, 0));
            var zero = Child(buret, "Vạch 0 ml", new Vector3(0, ZeroMarkY, 0));
            var mouth = Child(buret, "Miệng buret", new Vector3(0, 0.76f, 0));
            var tipLiquid = MeshObj(buret, "Dung dịch ở đầu buret",
                Lathe("DichDauBuret", 12, V(0, 0.004f), V(0.0011f, 0.004f), V(0.0022f, 0.035f), V(0.0047f, 0.062f), V(0.0047f, 0.07f), V(0, 0.07f)),
                Vector3.zero, naoh);
            NoShadow(tipLiquid);
            tipLiquid.SetActive(false);
            var column = MeshObj(buret, "Cột dung dịch", Lathe("CotDich", 16, V(0, 0), V(0.0047f, 0), V(0.0047f, 0), V(0.0047f, 1f), V(0.0047f, 1f), V(0, 1f)),
                                 new Vector3(0, BuretTubeBottom, 0), naoh);
            NoShadow(column);
            column.SetActive(false);
            var meniscus = Cyl(buret, "Mặt khum", new Vector3(0, ZeroMarkY, 0), 0.0049f, 0.0016f,
                               Mat("MatKhum", new Color(0.15f, 0.2f, 0.3f, 0.85f), 0.5f, 0, transparent: true));
            NoShadow(meniscus);
            meniscus.SetActive(false);
            var scaleMat = Mat("VachChiaBuret", Color.white, 0.1f, 0, BuretScaleTexture());
            MakeCutout(scaleMat);
            NoShadow(Panel(buret, "Vạch chia", new Vector3(0.0062f, (ScaleBottom + ScaleTop) / 2, 0), Vector3.right,
                           0.008f, ScaleTop - ScaleBottom, scaleMat));
            for (int ml = 0; ml <= 50; ml++)
                Label(buret, ml.ToString(), new Vector3(0.0064f, ZeroMarkY - ml * MetersPerMl, 0.0078f), Vector3.right, 0.0032f,
                      new Color(0.05f, 0.05f, 0.1f));
            Label(buret, "50 ml", new Vector3(0.0064f, ZeroMarkY + 0.025f, 0f), Vector3.right, 0.004f, new Color(0.05f, 0.05f, 0.1f));
            Box(buret, "Khóa buret", new Vector3(0, 0.05f, 0), new Vector3(0.012f, 0.012f, 0.05f), bluePlastic, false);
            Cyl(buret, "Thân khóa", new Vector3(0, 0.05f, 0), 0.008f, 0.02f, bluePlastic);
            var handle = new GameObject("Vùng vặn khóa");
            handle.transform.SetParent(buret, false);
            handle.transform.localPosition = new Vector3(0, 0.05f, 0);
            var hz = handle.AddComponent<BoxCollider>();
            hz.isTrigger = true;
            hz.size = new Vector3(0.06f, 0.05f, 0.08f);
            var stream = Group("Dòng chảy", buret, new Vector3(0, -0.012f, 0));
            NoShadow(Cyl(stream, "Dòng", new Vector3(0, -0.5f, 0), 0.0011f, 1f, naoh));
            stream.localScale = new Vector3(1, 0.1f, 1);
            stream.gameObject.SetActive(false);

            var buretBox = buret.gameObject.AddComponent<LabContainer>();
            buretBox.label = "Buret";
            buretBox.capacityMl = BuretTipMl + (BuretTubeTop - BuretTubeBottom) / MetersPerMl;
            buretBox.mouth = mouth;
            var stopcock = buret.gameObject.AddComponent<BuretStopcock>();
            stopcock.container = buretBox;
            stopcock.tip = tip;
            stopcock.zeroVolumeMl = BuretTipMl + (ZeroMarkY - BuretTubeBottom) / MetersPerMl;
            handle.AddComponent<StopcockHandle>().buret = stopcock;
            var bg = buret.gameObject.AddComponent<Grabbable>();
            bg.kind = "buret";
            bg.liesOnTable = true;
            bg.lengthM = 0.76f;
            bg.lyingRadiusM = 0.009f;
            bg.gripHeightM = 0.42f;
            var reading = buret.gameObject.AddComponent<MeniscusReading>();
            reading.buret = stopcock;
            reading.zeroMark = zero;
            reading.metersPerMl = MetersPerMl;
            reading.wallRadiusM = 0.0062f;
            var visual = buret.gameObject.AddComponent<BuretVisual>();
            visual.buret = stopcock;
            visual.zeroMark = zero;
            visual.metersPerMl = MetersPerMl;
            visual.liquidColumn = column.transform;
            visual.tipLiquid = tipLiquid;
            visual.meniscus = meniscus.transform;
            visual.stream = stream;
            visual.tubeTopLocalY = BuretTubeTop;
            visual.dropMaterial = Mat("GiotNaOH", new Color(0.75f, 0.88f, 1f, 0.6f), 0.95f, 0, transparent: true);

            // ---------- Pipet bầu 25 ml ----------
            var pip = Group("Pipet bầu 25 ml", g, L(0.27f, top + 0.017f, -1.2f));
            pip.localRotation = lying;
            var pipMesh = Lathe("PipetBau", 20, V(0, 0), V(0.0011f, 0), V(0.0018f, 0.01f), V(0.0032f, 0.02f), V(0.0032f, 0.13f),
                                V(0.0085f, 0.17f), V(0.0085f, 0.255f), V(0.0032f, 0.285f), V(0.0032f, 0.45f), V(0.0036f, 0.452f));
            NoShadow(MeshObj(pip, "Thủy tinh", pipMesh, Vector3.zero, labGlass, true));
            var pipLiquid = LiquidObject(pip, "Dung dịch trong pipet",
                new[] { V(0.0005f, 0.003f), V(0.001f, 0.01f), V(0.0024f, 0.02f), V(0.0024f, 0.13f), V(0.0077f, 0.17f), V(0.0077f, 0.255f),
                        V(0.0024f, 0.285f), V(0.0024f, 0.445f) },
                Mat("DungDichPipet", new Color(0.85f, 0.92f, 1f, 0.35f), 0.9f, 0, transparent: true));
            float markH = HeightForVolume(pipLiquid, 25f);
            Cyl(pip, "Vạch định mức", new Vector3(0, markH, 0), 0.0036f, 0.0008f, ringWhite);
            var bulbMesh = Lathe("QuaBop", 20, V(0, 0.445f), V(0.0042f, 0.445f), V(0.0042f, 0.46f), V(0.011f, 0.47f), V(0.0145f, 0.49f),
                                 V(0.013f, 0.512f), V(0.008f, 0.527f), V(0, 0.532f));
            MeshObj(pip, "Quả bóp cao su", bulbMesh, Vector3.zero, Mat("CaoSuDo", new Color(0.72f, 0.1f, 0.08f), 0.45f), true);
            Label(pip, "25 ml", new Vector3(0.0088f, 0.222f, 0), Vector3.right, 0.0055f, Color.white);
            Label(pip, "20°C", new Vector3(0.0088f, 0.205f, 0), Vector3.right, 0.004f, Color.white);
            var pipBox = pip.gameObject.AddComponent<LabContainer>();
            pipBox.label = "Pipet";
            pipBox.capacityMl = pipLiquid.VolumeTo(0.44f) * 1e6f;
            pipBox.fill = pipLiquid;
            pipBox.liquidRenderer = pipLiquid.GetComponent<Renderer>();
            var pipette = pip.gameObject.AddComponent<Pipette>();
            pipette.container = pipBox;
            pipette.tip = Child(pip, "Đầu pipet", Vector3.zero);
            pipette.markHeightM = markH;
            var pg = pip.gameObject.AddComponent<Grabbable>();
            pg.kind = "pipet";
            pg.liesOnTable = true;
            pg.lengthM = 0.53f;
            pg.lyingRadiusM = 0.0145f;
            pg.gripHeightM = 0.38f;

            // ---------- Bình nón (rỗng) ----------
            var flask = Vessel(g, "Bình nón", L(0.30f, top, -0.25f), Erlenmeyer(), null, 250f, 0.143f, "binh_non", 0.11f,
                               Mat("DungDichBinhNon", new Color(0.85f, 0.92f, 1f, 0.3f), 0.9f, 0, transparent: true));
            flask.slowMixing = true;
            var swirl = flask.gameObject.AddComponent<SwirlDetector>();
            flask.swirl = swirl;
            flask.gameObject.AddComponent<FlaskSwirlVisual>().swirl = swirl;

            // ---------- Cốc HCl (hóa chất cần xác định nồng độ) ----------
            var hcl = Vessel(g, "Cốc HCl", L(-0.12f, top, 0.26f), Beaker("Coc250", 0.035f, 0.1f),
                             new[] { V(0.0335f, 0.002f), V(0.0335f, 0.1f) }, 250f, 0.1f, "coc", 0.07f,
                             Mat("DungDichHCl", new Color(0.85f, 0.95f, 0.9f, 0.3f), 0.9f, 0, transparent: true));
            hcl.isStock = true;
            titrAnalyteLabel = Label(hcl.transform, "HCl", new Vector3(0.0365f, 0.065f, 0), Vector3.right, 0.012f, new Color(0.7f, 0.05f, 0.05f)).GetComponent<TextMesh>();

            // ---------- Chai NaOH 0,1 M ----------
            var bottleMesh = Lathe("ChaiNaOH", 24, V(0, 0), V(0.0365f, 0), V(0.0365f, 0), V(0.037f, 0.004f), V(0.037f, 0.14f), V(0.032f, 0.158f),
                                   V(0.015f, 0.178f), V(0.014f, 0.183f), V(0.014f, 0.205f), V(0.0155f, 0.207f));
            var naohBottle = Vessel(g, "Chai NaOH 0,1 M", L(-0.24f, top, 0.44f), bottleMesh,
                new[] { V(0.035f, 0.003f), V(0.0355f, 0.006f), V(0.0355f, 0.14f), V(0.0305f, 0.157f), V(0.0135f, 0.177f), V(0.0125f, 0.182f), V(0.0125f, 0.205f) },
                520f, 0.207f, "chai", 0.13f, naoh);
            Box(naohBottle.transform, "Nhãn chai", new Vector3(0.0372f, 0.08f, 0), new Vector3(0.002f, 0.05f, 0.04f), signWhite, false);
            titrTitrantLabel = Label(naohBottle.transform, "NaOH\n0,1 M", new Vector3(0.0385f, 0.08f, 0), Vector3.right, 0.009f, new Color(0.05f, 0.15f, 0.5f)).GetComponent<TextMesh>();

            // ---------- Cốc thải ----------
            var waste = Vessel(g, "Cốc thải", L(0.25f, top, 0.42f), Beaker("Coc100", 0.025f, 0.07f),
                               new[] { V(0.0235f, 0.002f), V(0.0235f, 0.07f) }, 100f, 0.07f, "coc", 0.05f, naoh);
            Label(waste.transform, "Thải", new Vector3(0.0265f, 0.045f, 0), Vector3.right, 0.01f, new Color(0.3f, 0.3f, 0.3f));

            // ---------- Lọ phenolphtalein ----------
            var dropRoot = Group("Lọ phenolphtalein", g, L(-0.08f, top, -0.32f));
            MeshObj(dropRoot, "Lọ", Lathe("ChaiNhoGiot", 16, V(0, 0), V(0.017f, 0), V(0.017f, 0), V(0.018f, 0.003f), V(0.018f, 0.05f), V(0.012f, 0.058f), V(0.007f, 0.062f), V(0, 0.062f)), Vector3.zero, amber, true);
            Cyl(dropRoot, "Núm cao su", new Vector3(0, 0.075f, 0), 0.007f, 0.028f, rubber);
            Label(dropRoot, "P.P", new Vector3(0.0185f, 0.03f, 0), Vector3.right, 0.008f, Color.white);
            var dropper = dropRoot.gameObject.AddComponent<Dropper>();
            titrDropper = dropper;
            dropper.tip = Child(dropRoot, "Đầu nhỏ giọt", new Vector3(0, 0.09f, 0));
            var dg = dropRoot.gameObject.AddComponent<Grabbable>();
            dg.kind = "lo_nho_giot";
            dg.gripHeightM = 0.05f;

            // ---------- Bình tia nước cất ----------
            var washRoot = Group("Bình tia nước cất", g, L(0.06f, top, 0.5f));
            WashBottle(washRoot, Vector3.zero);
            var wash = washRoot.gameObject.AddComponent<Khkt.Interaction.WashBottle>();
            wash.nozzle = Child(washRoot, "Miệng vòi", new Vector3(0.045f, 0.245f, 0));
            var wg = washRoot.gameObject.AddComponent<Grabbable>();
            wg.kind = "binh_tia";
            wg.gripHeightM = 0.1f;

            demoBuret = stopcock;
            demoFlask = flask;
            demoHcl = hcl;
            demoNaoh = naohBottle;
            demoWaste = waste;
            demoPipette = pipette;
            demoReading = reading;
            demoSwirl = swirl;
        }

        /// <summary>
        /// Bộ thí nghiệm định tính (bàn 2): giá 6 ống nghiệm dùng được, cốc, bình nón, đèn cồn, bình tia,
        /// và các thuốc thử hay dùng (lọ thật trong danh mục) trên giá hóa chất của bàn.
        /// </summary>
        static void QualitativeSet(Transform bench, int s)
        {
            const float top = 0.8f;
            var g = Group("Bộ thí nghiệm định tính", bench);
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z * s);
            var rack = Group("Giá ống nghiệm", g, L(0.08f, top, -0.75f));
            Box(rack, "Đế giá", new Vector3(0, 0.005f, 0), new Vector3(0.09f, 0.01f, 0.3f), oak, true);
            Box(rack, "Tấm đỡ có lỗ", new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.008f, 0.3f), oak, false);
            foreach (float z in new[] { -0.145f, 0.145f })
                Box(rack, "Thành giá", new Vector3(0, 0.04f, z), new Vector3(0.09f, 0.08f, 0.01f), oak, false);
            for (int i = 0; i < 6; i++)
                FunctionalVessel(g, $"Ống nghiệm {i + 1}", L(0.08f, top + 0.011f, -0.75f - 0.115f + i * 0.046f), "ong_nghiem");
            FunctionalVessel(g, "Cốc có mỏ 100 ml", L(0.25f, top, -0.3f), "coc100");
            FunctionalVessel(g, "Cốc có mỏ 100 ml (2)", L(0.25f, top, -0.12f), "coc100");
            FunctionalVessel(g, "Cốc có mỏ 250 ml", L(0.1f, top, 0.05f), "coc250");
            FunctionalVessel(g, "Bình tam giác 250 ml", L(0.28f, top, 0.25f), "binh_non");
            AlcoholLamp(g, L(0.25f, top, -0.55f));
            var washRoot = Group("Bình tia nước cất", g, L(-0.05f, top, 0.4f));
            WashBottle(washRoot, Vector3.zero);
            var wash = washRoot.gameObject.AddComponent<Khkt.Interaction.WashBottle>();
            wash.nozzle = Child(washRoot, "Miệng vòi", new Vector3(0.045f, 0.245f, 0));
            var wg = washRoot.gameObject.AddComponent<Grabbable>();
            wg.kind = "binh_tia";
            wg.gripHeightM = 0.1f;

            // Thuốc thử hay dùng: NaOH, HCl, AgNO3, BaCl2, CuSO4, FeCl3, KSCN, phenolphtalein, quỳ tím
            int[] reagents = { 124, 98, 230, 155, 253, 159, 351, 394, 393 };
            var byStt = (chemData.hoa_chat ?? new ChemEntry[0]).ToDictionary(c => c.stt);
            for (int i = 0; i < reagents.Length; i++)
            {
                if (!byStt.TryGetValue(reagents[i], out var e)) continue;
                var t = ChemBottleObject(g, e, L(-0.36f, 1.15f, -1.05f + i * 0.2f), new Color(0.05f, 0.58f, 0.62f));
                t.localRotation = Quaternion.Euler(0, 180, 0);  // nhãn quay về phía học sinh (+x)
            }
            Label(g, "BỘ THÍ NGHIỆM ĐỊNH TÍNH", L(0.452f, 0.74f, 0), Vector3.right, 0.03f, Color.white);
        }

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>Vật chứa cầm được: vỏ thủy tinh (có va chạm), khối dung dịch theo thể tích, miệng bình.</summary>
        static LabContainer Vessel(Transform parent, string name, Vector3 localPos, Mesh glassMesh, Vector2[] innerProfile,
                                   float capacityMl, float mouthY, string kind, float gripHeight, Material liquidMat)
        {
            var root = Group(name, parent, localPos);
            NoShadow(MeshObj(root, "Thủy tinh", glassMesh, Vector3.zero, labGlass, true));
            var fill = LiquidObject(root, "Dung dịch", innerProfile, liquidMat);
            var c = root.gameObject.AddComponent<LabContainer>();
            c.label = name;
            c.capacityMl = capacityMl;
            c.fill = fill;
            c.liquidRenderer = fill.GetComponent<Renderer>();
            c.mouth = Child(root, "Miệng", new Vector3(0, mouthY, 0));
            var gr = root.gameObject.AddComponent<Grabbable>();
            gr.kind = kind;
            gr.gripHeightM = gripHeight;
            return c;
        }

        static LiquidFill LiquidObject(Transform parent, string name, Vector2[] innerProfile, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            NoShadow(go);
            var fill = go.AddComponent<LiquidFill>();
            if (innerProfile != null) fill.innerProfile = innerProfile;
            fill.volumeMl = 0f;
            return fill;
        }

        /// <summary>Độ cao mực chất lỏng ứng với thể tích ml trong vật có thành trong fill.innerProfile.</summary>
        static float HeightForVolume(LiquidFill fill, float ml)
        {
            float lo = fill.innerProfile[0].y, hi = fill.innerProfile[fill.innerProfile.Length - 1].y;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) / 2;
                if (fill.VolumeTo(mid) * 1e6f < ml) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        static PlaceSocket Socket(Transform parent, string label, Vector3 localPos, string[] accepts, bool mounts,
                                  Vector3 zoneCenter, Vector3 zoneSize, Vector3 markerPos, float markerRadius, Material ringMat)
        {
            var t = Group("Chỗ đặt: " + label, parent, localPos);
            var s = t.gameObject.AddComponent<PlaceSocket>();
            s.label = label;
            s.accepts = accepts;
            s.mountsBuret = mounts;
            var z = t.gameObject.AddComponent<BoxCollider>();
            z.isTrigger = true;
            z.center = zoneCenter;
            z.size = zoneSize;
            z.enabled = false;
            s.zone = z;
            var ring = MeshObj(t, "Vòng sáng", Lathe("VongSang", 32, V(0.042f, 0), V(0.05f, 0), V(0.05f, 0), V(0.05f, 0.002f),
                                                     V(0.05f, 0.002f), V(0.042f, 0.002f), V(0.042f, 0.002f), V(0.042f, 0)), markerPos, ringMat);
            ring.transform.localScale = new Vector3(markerRadius / 0.05f, 1, markerRadius / 0.05f);
            NoShadow(ring);
            ring.SetActive(false);
            s.marker = ring;
            return s;
        }

        /// <summary>Vạch chia buret 0–50 ml: vạch dài mỗi 1 ml, vừa mỗi 0,5 ml, ngắn mỗi 0,1 ml. Nền trong suốt.</summary>
        static Texture2D BuretScaleTexture()
        {
            const int w = 64, h = 4096;
            float pxPerTenth = MetersPerMl / 10f / (ScaleTop - ScaleBottom) * h;
            var ink = new Color(0.05f, 0.05f, 0.1f, 1f);
            var clear = new Color(1f, 1f, 1f, 0f);
            return Tex("VachBuret", w, h, (x, y) =>
            {
                float yLocal = ScaleBottom + (y + 0.5f) / h * (ScaleTop - ScaleBottom);
                float tenths = (ZeroMarkY - yLocal) / MetersPerMl * 10f;
                if (tenths < -0.5f || tenths > 500.5f) return clear;
                float nearest = Mathf.Round(tenths);
                if (Mathf.Abs(tenths - nearest) * pxPerTenth > 1.1f) return clear;
                int n = (int)nearest;
                float len = n % 10 == 0 ? 1f : n % 5 == 0 ? 0.65f : 0.38f;
                return x < len * w ? ink : clear;
            }, repeat: false);
        }

        static void MakeCutout(Material m)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        // =====================================================================
        // Hệ thống demo (bàn tay ảo, giao diện kính mờ, kính lúp, quy trình)
        // =====================================================================

        static void BuildDemo(Transform root)
        {
            var g = new GameObject("Hệ thống demo");
            g.transform.SetParent(root, false);
            demoReading.eye = viewerCamera.transform;

            var catalog = g.AddComponent<ChemicalCatalog>();
            catalog.chemicalsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ChemJsonPath);
            catalog.equipmentJson = AssetDatabase.LoadAssetAtPath<TextAsset>(EquipJsonPath);
            catalog.boilingJson = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/nhiet_do_soi.json");
            g.AddComponent<Khkt.Assembly.FluidNetwork>();
            var fx = g.AddComponent<ReactionFx>();
            fx.bubbleMaterial = Mat("BotKhi", new Color(1f, 1f, 1f, 0.55f), 0.95f, 0, transparent: true);
            fx.fumeMaterial = Mat("KhoiKhi", new Color(0.9f, 0.9f, 0.9f, 0.3f), 0.1f, 0, transparent: true);

            var proc = g.AddComponent<LabProcedure>();
            proc.buret = demoBuret;
            proc.clampSocket = clampSocket;
            proc.underSocket = underSocket;
            proc.flask = demoFlask;
            proc.hclStock = demoHcl;
            proc.naohStock = demoNaoh;
            proc.wasteBeaker = demoWaste;
            proc.pipette = demoPipette;
            proc.reading = demoReading;
            proc.swirl = demoSwirl;
            proc.indicatorDropper = titrDropper;
            proc.analyteLabel = titrAnalyteLabel;
            proc.titrantLabel = titrTitrantLabel;

            var loupeGo = new GameObject("Camera kính lúp");
            loupeGo.transform.SetParent(g.transform, false);
            loupeGo.AddComponent<Camera>().enabled = false;
            var loupe = loupeGo.AddComponent<LoupeCamera>();
            loupe.reading = demoReading;
            loupe.eye = viewerCamera.transform;

            var handGo = new GameObject("Bàn tay ảo");
            handGo.transform.SetParent(g.transform, false);
            var rig = handGo.AddComponent<HandRig>();

            var outline = g.AddComponent<HoverOutline>();
            outline.outlineShader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/VienSang.shader");

            var ui = g.AddComponent<ModernUI>();
            ui.viewCamera = viewerCamera;
            ui.glassShader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/KinhMo.shader");
            ui.procedure = proc;
            ui.reading = demoReading;
            ui.loupe = loupe;

            var hand = g.AddComponent<HandController>();
            hand.viewCamera = viewerCamera;
            hand.hand = rig;
            hand.outline = outline;
            hand.procedure = proc;
            hand.ui = ui;
            ui.hand = hand;
            // Lắp ráp tự do (giai đoạn C): bóng mờ xem trước chỗ lắp, kiểm tra trọng tâm bộ lắp
            hand.ghostMaterial = Mat("BongMoLapRap", new Color(0.3f, 0.95f, 1f, 0.32f), 0.5f, 0, emission: new Color(0.15f, 0.6f, 0.75f), transparent: true);
            g.AddComponent<Khkt.Assembly.AssemblyManager>();
            BuildWorkshop(root, viewerCamera, hand.ghostMaterial, outline.outlineShader, AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/KinhMo.shader"));

            var glassShader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Shaders/KinhMo.shader");
            g.AddComponent<AiClient>();
            g.AddComponent<Khkt.Input.SerialControllerSource>().portName = "auto";  // tay cầm cắm USB / Bluetooth (firmware/tay_cam_esp32cam)
            g.AddComponent<Khkt.Input.UdpControllerSource>();  // tay cầm Wi-Fi (bản có ADS1115) và công cụ giả lập tools/tay_cam_udp.py
            var viewer = viewerCamera.GetComponentInParent<DesktopViewer>();
            var robot = BuildHumanoid(root, RobotDock);
            robot.transform.localRotation = Quaternion.Euler(0, 180, 0);
            robot.viewerCamera = viewerCamera.transform;
            robot.puddleMaterial = Mat("VetTran", new Color(0.75f, 0.85f, 0.95f, 0.55f), 0.95f, 0, transparent: true);
            var search = g.AddComponent<ChemSearchUI>();
            search.ui = ui;
            search.viewer = viewer;
            search.beamMaterial = Mat("CotSang", new Color(1f, 0.7f, 0.2f, 0.35f), 0.2f, 0, emission: new Color(2.5f, 1.4f, 0.3f), transparent: true);
            search.outlineShader = outline.outlineShader;
            var ann = g.AddComponent<ReactionAnnotator>();
            ann.ui = ui;
            var chat = g.AddComponent<RobotChatUI>();
            chat.ui = ui;
            chat.hand = hand;
            chat.procedure = proc;
            chat.viewer = viewerCamera.transform;
            var intro = g.AddComponent<IntroScreen>();
            intro.ui = ui;
            intro.procedure = proc;
            intro.viewer = viewer;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;  // máy chủ AI cục bộ http://127.0.0.1
        }

        /// <summary>Gắn PlaceSurface cho các mặt bàn để đặt dụng cụ xuống chỗ nào cũng được.</summary>
        static void AttachSurfaces(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if ((t.name == "Mặt bàn" || t.name.StartsWith("Mặt bàn đoạn") || t.name == "Mặt làm việc") && t.GetComponent<Collider>() != null)
                    t.gameObject.AddComponent<PlaceSurface>();
        }

        // =====================================================================
        // Thẻ thông tin dụng cụ
        // =====================================================================

        static readonly (string match, string title, string desc, string usage)[] InfoTable =
        {
            ("Pipet bầu", "Pipet bầu 25 ml",
                "Lấy chính xác 25,00 ml dung dịch. Chỉ có một vạch định mức trên cổ.",
                "Hút quá vạch một chút, xả bớt cho đáy mặt khum chạm vạch, rồi xả hết vào bình nón."),
            ("Cốc HCl", "Cốc HCl (chưa biết nồng độ)",
                "Dung dịch cần xác định nồng độ bằng chuẩn độ. Nhúng pipet vào đây để hút.", ""),
            ("Chai NaOH", "Chai NaOH 0,1 M",
                "Dung dịch chuẩn đã biết nồng độ. Rót vào buret đã lắp trên giá.", "NaOH ăn da: đeo kính, găng tay."),
            ("Cốc thải", "Cốc thải",
                "Hứng dung dịch xả bỏ, ví dụ khi chỉnh mặt khum buret về vạch 0.", ""),
            ("Lọ phenolphtalein", "Lọ phenolphtalein",
                "Chất chỉ thị: không màu trong axit, hồng khi pH > 8,2. Nhỏ 2–3 giọt vào bình nón.", ""),
            ("Buret", "Buret 50 ml",
                "Ống thủy tinh dài, chia vạch 0,1 ml, số 0 ở trên cùng. Dùng để nhỏ chính xác dung dịch chuẩn khi chuẩn độ.",
                "Tráng buret bằng chính dung dịch sẽ đựng. Đọc ở đáy mặt khum, mắt ngang vạch; đọc đến 0,05 ml."),
            ("Khóa buret", "Khóa buret",
                "Điều chỉnh tốc độ chảy: mở lớn để chảy thành dòng, mở rất nhỏ để nhỏ từng giọt.",
                "Gần điểm tương đương chỉ nhỏ từng giọt. Đọc thể tích khi khóa đã đóng."),
            ("Bình nón", "Bình nón 250 ml",
                "Đựng dung dịch cần chuẩn độ. Miệng hẹp nên lắc tròn mạnh mà không bắn ra ngoài.",
                "Vừa nhỏ vừa lắc tròn đều. Điểm cuối với phenolphtalein: hồng nhạt bền khoảng 30 giây."),
            ("Cốc NaOH", "Cốc thủy tinh đựng NaOH 0,1 M",
                "Dung dịch chuẩn dùng để nạp vào buret.",
                "NaOH ăn da: đeo kính bảo hộ, găng tay; dính vào da thì rửa ngay bằng nhiều nước."),
            ("Đế giá sắt", "Giá sắt (giá đỡ)",
                "Đế nặng và cọc thẳng đứng, có kẹp giữ buret.",
                "Kẹp buret thẳng đứng, đầu buret ở trong miệng bình nón khoảng 1–2 cm."),
            ("Gạch men trắng", "Tấm gạch men trắng",
                "Lót dưới bình nón để dễ nhận ra lúc chất chỉ thị đổi màu.", ""),
            ("Bình tia nước cất", "Bình tia nước cất",
                "Bóp bình để tia nước cất.",
                "Tráng thành bình nón khi có giọt dung dịch bám ở thành hoặc đầu buret."),
            ("Giá ống nghiệm", "Giá ống nghiệm",
                "Giữ ống nghiệm đứng thẳng. Ống nghiệm dùng làm thí nghiệm định tính với lượng nhỏ hóa chất.",
                "Đun ống nghiệm: hơ nóng đều, hướng miệng ống ra chỗ không có người."),
            ("Đèn cồn", "Đèn cồn",
                "Dùng để đun nóng.",
                "Châm bằng que diêm, không châm từ đèn cồn khác. Tắt bằng cách đậy nắp, không thổi."),
            ("Cốc thủy tinh 250 ml", "Cốc thủy tinh (cốc có mỏ)",
                "Đựng, pha, đun nóng dung dịch. Vạch trên cốc chỉ để ước lượng.",
                "Không dùng cốc để đo thể tích chính xác; dùng pipet, buret hoặc bình định mức."),
            ("Cốc thủy tinh", "Cốc thủy tinh (cốc có mỏ)",
                "Đựng, pha, đun nóng dung dịch. Vạch trên cốc chỉ để ước lượng.", ""),
            ("Bình cầu", "Bình cầu",
                "Đáy tròn chịu nhiệt đều, dùng đun nóng, điều chế, chưng cất.", "Đun qua lưới tản nhiệt, không đun khô."),
            ("Ống đong", "Ống đong",
                "Đo thể tích chất lỏng gần đúng, kém chính xác hơn buret và pipet.", "Đọc ở đáy mặt khum, mắt ngang vạch."),
            ("Bình định mức", "Bình định mức",
                "Pha dung dịch có nồng độ chính xác.",
                "Thêm nước cất đến khi đáy mặt khum chạm vạch mức, đậy nút rồi lắc đều."),
            ("Cân điện tử", "Cân điện tử",
                "Cân khối lượng hóa chất rắn.", "Đặt giấy cân hoặc cốc lên đĩa, trừ bì về 0 rồi mới cho hóa chất."),
            ("(chai nâu)", "Chai thủy tinh màu nâu",
                "Đựng hóa chất dễ bị ánh sáng phân hủy, ví dụ AgNO3, KMnO4.", "Đậy nắp ngay sau khi lấy; nhãn quay ra ngoài."),
            ("(chai nhỏ giọt)", "Lọ nhỏ giọt",
                "Đựng chất chỉ thị (phenolphtalein, metyl da cam, quỳ).", "Mỗi lần dùng 1–2 giọt."),
            ("(lọ rộng miệng)", "Lọ rộng miệng",
                "Đựng hóa chất rắn (muối, oxit, kim loại dạng hạt).", "Lấy bằng thìa sạch, không dùng tay."),
            ("(chai trong)", "Chai đựng dung dịch chuẩn",
                "Dung dịch đã biết chính xác nồng độ, dùng để chuẩn độ.", ""),
            ("Chai nước rửa tay", "Nước rửa tay", "Rửa tay sau khi làm thí nghiệm.", ""),
            ("(chai nhựa)", "Chai nhựa đựng dung dịch",
                "Đựng dung dịch kiềm (NaOH ăn mòn thủy tinh nên hay đựng chai nhựa).", ""),
            ("Can nhựa", "Can nhựa", "Đựng nước cất hoặc dung dịch số lượng lớn.", ""),
            ("Tủ hút khí độc", "Tủ hút khí độc",
                "Làm thí nghiệm sinh ra khí độc hoặc mùi (Cl2, NO2, H2S, HCl đặc...). Quạt hút khí ra ngoài qua ống.",
                "Kéo cửa kính xuống thấp khi làm; không thò đầu vào trong tủ."),
            ("Vòi rửa mắt", "Vòi rửa mắt khẩn cấp",
                "Khi hóa chất bắn vào mắt.", "Giữ mắt mở, rửa liên tục bằng nước sạch ít nhất 15 phút rồi báo giáo viên."),
            ("Bình chữa cháy", "Bình chữa cháy",
                "Dập đám cháy nhỏ.", "Rút chốt, hướng vòi vào gốc lửa, bóp cò, đứng đầu hướng gió."),
            ("Hộp y tế", "Hộp sơ cứu", "Sơ cứu bỏng nhẹ, vết cắt nhỏ.", "Báo ngay giáo viên khi có tai nạn."),
            ("Xô cát", "Xô cát",
                "Dập lửa do xăng dầu, cồn tràn hoặc kim loại hoạt động mạnh (như Na) cháy.", "Không dùng nước với các đám cháy này."),
            ("Thùng chất thải hóa học", "Thùng chất thải hóa học",
                "Chứa hóa chất thải sau thí nghiệm.", "Không đổ hóa chất độc, kim loại nặng xuống bồn rửa."),
            ("Thùng rác", "Thùng rác", "Rác thường (giấy, găng tay không dính hóa chất độc).", ""),
            ("Bồn rửa chung", "Bồn rửa chung",
                "Rửa dụng cụ sau thí nghiệm.", "Rửa bằng nước máy rồi tráng lại bằng nước cất."),
            ("Bồn rửa", "Bồn rửa", "Rửa dụng cụ.", "Không đổ hóa chất độc, kim loại nặng xuống bồn."),
            ("Vòi nước", "Vòi nước", "Nước máy để rửa dụng cụ.", ""),
            ("Giá treo dụng cụ thủy tinh", "Giá treo dụng cụ thủy tinh",
                "Úp ngược dụng cụ đã rửa lên chốt cho ráo nước.", ""),
            ("Giá treo áo blouse", "Giá treo áo blouse",
                "Mặc áo blouse khi vào phòng thí nghiệm, treo lại khi ra.", "Buộc tóc gọn, không mặc áo rộng tay."),
            ("Tủ axit", "Tủ đựng axit",
                "Bảo quản axit (HCl, H2SO4, HNO3) riêng, có ống thông gió.",
                "Không để chung với bazơ và chất dễ cháy. Pha loãng: rót từ từ axit vào nước, không làm ngược lại."),
            ("Tủ chất dễ cháy", "Tủ chất dễ cháy", "Bảo quản cồn, axeton, ete...", "Để xa nguồn lửa, đóng kín."),
            ("Tủ dụng cụ thủy tinh", "Tủ dụng cụ thủy tinh", "Cất bình cầu, cốc, bình nón, ống đong, ống nghiệm.", ""),
            ("Kệ hóa chất", "Kệ hóa chất",
                "Hóa chất xếp theo nhóm, có nhãn.", "Lấy xong để lại đúng chỗ; không dùng hóa chất mất nhãn."),
            ("Bàn pha chế", "Bàn pha chế", "Cân, pha dung dịch trước buổi thực hành.", ""),
            ("Quạt hút", "Quạt hút", "Thông gió phòng hóa chất, đẩy hơi hóa chất ra ngoài.", ""),
            ("Khung bảng tuần hoàn", "Bảng tuần hoàn các nguyên tố hóa học",
                "Màu theo khối: s (đỏ cam), p (vàng), d (xanh dương), f (xanh lá), khí hiếm (tím).", ""),
            ("Mặt bảng", "Bảng trắng", "Giáo viên ghi quy trình và số liệu chung của lớp.", ""),
            ("Máy tính xách tay", "Máy tính của giáo viên",
                "Sau này hiện bảng giám sát: thao tác và số liệu của từng nhóm.", ""),
            ("Bàn giáo viên", "Bàn giáo viên", "Làm thí nghiệm biểu diễn.", ""),
            ("Bàn thí nghiệm", "Bàn thí nghiệm học sinh",
                "Mặt bàn epoxy chịu hóa chất, có bồn rửa, giá hóa chất. Mỗi bàn một nhóm 3 bạn.", ""),
            ("Công tắc đèn", "Công tắc đèn", "Bật/tắt đèn trần phòng thí nghiệm.", ""),
        };

        static void AttachEquipmentInfo(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Chữ:") || t.GetComponent<EquipmentInfo>() != null) continue;
                foreach (var (match, title, desc, usage) in InfoTable)
                {
                    bool hit = match.StartsWith("(") ? t.name.Contains(match) : t.name == match || t.name.StartsWith(match + " ");
                    if (!hit) continue;
                    var e = t.gameObject.AddComponent<EquipmentInfo>();
                    e.title = title;
                    e.description = desc;
                    e.usage = usage;
                    EnsureCollider(t);
                    break;
                }
            }
        }

        static void EnsureCollider(Transform t)
        {
            if (t.GetComponentInChildren<Collider>(true) != null) return;
            var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = t.InverseTransformPoint(b.center);
            Vector3 ls = t.lossyScale;
            c.size = new Vector3(b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                                 b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        }

        // =====================================================================
        // Thao tác bật/tắt
        // =====================================================================

        static void AttachUsables(Transform root)
        {
            Physics.SyncTransforms();
            var flameMat = Mat("LuaDenCon", new Color(1f, 0.6f, 0.15f), 0.2f, 0, emission: new Color(4f, 1.8f, 0.4f));
            var waterMat = Mat("NuocChay", new Color(0.75f, 0.88f, 1f, 0.45f), 0.95f, 0, transparent: true);
            var flameMesh = Lathe("NgonLua", 12, V(0, 0), V(0.006f, 0.004f), V(0.009f, 0.012f), V(0.007f, 0.024f), V(0.003f, 0.034f), V(0, 0.04f));

            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                switch (t.name)
                {
                    case "Đèn cồn":
                    {
                        var flame = MeshObj(t, "Ngọn lửa", flameMesh, new Vector3(0, 0.077f, 0), flameMat);
                        NoShadow(flame);
                        var light = new GameObject("Ánh lửa").AddComponent<Light>();
                        light.transform.SetParent(flame.transform, false);
                        light.transform.localPosition = new Vector3(0, 0.03f, 0);
                        light.type = LightType.Point;
                        light.range = 0.9f;
                        light.color = new Color(1f, 0.65f, 0.3f);
                        light.intensity = 0.7f;
                        light.shadows = LightShadows.None;
                        flame.AddComponent<FlameFlicker>().flameLight = light;
                        flame.SetActive(false);
                        var cap = t.parent.Cast<Transform>().FirstOrDefault(c => c.name == "Nắp đèn cồn"
                                  && Vector2.Distance(new Vector2(c.position.x, c.position.z), new Vector2(t.position.x, t.position.z)) < 0.02f);
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.showWhenOn = new[] { flame };
                        toggle.hideWhenOn = cap != null ? new[] { cap.gameObject } : new GameObject[0];
                        toggle.onHint = "Bấm chuột trái: mở nắp, châm đèn cồn";
                        toggle.offHint = "Bấm chuột trái: đậy nắp để tắt (không thổi)";
                        var heat = t.gameObject.AddComponent<HeatSource>();
                        heat.toggle = toggle;
                        heat.powerW = 250f;
                        heat.zone = Child(t, "Vùng nóng ngọn lửa", new Vector3(0, 0.2f, 0));
                        heat.zoneSize = new Vector3(0.12f, 0.2f, 0.12f);
                        break;
                    }
                    case "Vòi nước":
                    {
                        var spout = t.Find("Miệng vòi");
                        if (spout == null) break;
                        Vector3 from = spout.position + Vector3.down * 0.03f;
                        float len = Physics.Raycast(from, Vector3.down, out RaycastHit hit, 0.8f) ? hit.distance : 0.35f;
                        var stream = Group("Dòng nước", t);
                        stream.position = from;
                        NoShadow(Cyl(stream, "Nước", new Vector3(0, -len / 2, 0), 0.005f, len, waterMat));
                        stream.gameObject.SetActive(false);
                        EnsureCollider(t);
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.showWhenOn = new[] { stream.gameObject };
                        toggle.onHint = "Bấm chuột trái: mở vòi";
                        toggle.offHint = "Bấm chuột trái: khóa vòi";
                        break;
                    }
                    case "Vòi rửa mắt":
                    {
                        var jets = Group("Nước rửa mắt", t);
                        foreach (float z in new[] { -0.05f, 0.05f })
                            NoShadow(Cyl(jets, "Tia nước", new Vector3(0, 1.13f, z * 0.6f), 0.006f, 0.12f, waterMat, false,
                                         new Vector3(z > 0 ? 18 : -18, 0, 0)));
                        jets.gameObject.SetActive(false);
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.showWhenOn = new[] { jets.gameObject };
                        toggle.onHint = "Bấm chuột trái: gạt cần cho nước phun";
                        toggle.offHint = "Bấm chuột trái: tắt nước";
                        break;
                    }
                    case "Công tắc đèn":
                    {
                        var lights = root.Find("Phòng thí nghiệm/Đèn trần");
                        if (lights == null) break;
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.isOn = true;
                        toggle.lights = lights.GetComponentsInChildren<Light>(true);
                        toggle.showWhenOn = lights.GetComponentsInChildren<Transform>(true)
                            .Where(x => x.name == "Bóng tuýp" || x.name == "Dải LED trần").Select(x => x.gameObject).ToArray();
                        toggle.onHint = "Bấm chuột trái: bật đèn";
                        toggle.offHint = "Bấm chuột trái: tắt đèn";
                        break;
                    }
                    case "Công tắc tủ hút":
                    {
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.isOn = true;
                        toggle.onHint = "Bấm chuột trái: bật quạt tủ hút";
                        toggle.offHint = "Bấm chuột trái: tắt quạt tủ hút";
                        var zone = t.parent.GetComponentInChildren<FumeHoodZone>();
                        if (zone != null) zone.fan = toggle;
                        var info = t.gameObject.AddComponent<EquipmentInfo>();
                        info.title = "Công tắc quạt tủ hút";
                        info.description = "Quạt chạy thì khí độc trong tủ được hút ra ngoài.";
                        break;
                    }
                    case "Quạt hút":
                    {
                        var toggle = t.gameObject.AddComponent<ToggleEffect>();
                        toggle.isOn = true;
                        toggle.fans = t.GetComponentsInChildren<CeilingFan>(true);
                        toggle.onHint = "Bấm chuột trái: bật quạt hút";
                        toggle.offHint = "Bấm chuột trái: tắt quạt hút";
                        break;
                    }
                }
            }
        }
    }
}
