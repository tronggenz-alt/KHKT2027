using System.Collections.Generic;
using Khkt.Demo.Instruments;
using Khkt.Interaction.Bio;
using Khkt.Simulation.Bio;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo.Bio
{
    public class MicroscopeProgram : MicroscopeCameraProgram
    {
        public MicroscopeProgram(MicroscopeController mic = null) : base(mic) { }
    }

    /// <summary>
    /// Phần mềm vận hành Kính hiển vi quang học hai mắt & Kính hiển vi kỹ thuật số giáo viên (kinh_hien_vi).
    /// Hỗ trợ 4 vật kính (4×, 10×, 40×, 100× dầu), ốc đại cấp/vi cấp, bàn sa trượt X–Y, nhỏ dầu soi 100×,
    /// 11 tiêu bản chuẩn + tiêu bản thực hành động (Osmosis.cs), chiếu HDMI lên t2_tuong_video_2 và chụp ảnh PNG.
    /// </summary>
    public class MicroscopeCameraProgram : Khkt.Demo.Instruments.InstrumentProgram
    {
        public override string Code => "kinh_hien_vi";
        readonly MicroscopeController hostMicroscope;

        int markedCells = 0;
        int slideIdx = 0;
        double measuredLengthUm = 225.0;
        string lastSavedPng = "";
        bool showStructureSchematic = false;
        Button bLamp, bObj, bSlide, bCoarse, bFinePlus, bFineMinus, bOil, bPlasmolysis, bDeplasmolysis, bStain, bMeasure, bCapture, bViewTab;
        Text statusText;

        public MicroscopeCameraProgram(MicroscopeController mic = null)
        {
            hostMicroscope = mic;
            if (hostMicroscope != null)
            {
                for (int i = 0; i < MicroscopeController.StandardSlides.Length; i++)
                {
                    if (MicroscopeController.StandardSlides[i].id == hostMicroscope.activeSlideCode)
                    {
                        slideIdx = i;
                        break;
                    }
                }
            }
        }

        public override void Build()
        {
            if (Doc == null || Doc.Steps.Count == 0)
                Doc = BioCatalogLoader.LoadMachineDoc(Code);

            bLamp = S.AddButton("Đèn LED đế", 0, 0, () =>
            {
                if (hostMicroscope != null) hostMicroscope.lampOn = !hostMicroscope.lampOn;
                S.Done("bat_den");
            });
            bObj = S.AddButton("Đổi vật kính (4×..100×)", 1, 0, () =>
            {
                if (hostMicroscope != null) hostMicroscope.CycleObjective();
                S.Done("chon_vat_kinh");
                S.Done("vat_kinh_4x");
                S.Done("vat_kinh_10x_40x");
            });
            bSlide = S.AddButton("Đổi tiêu bản (11 mẫu)", 2, 0, () =>
            {
                slideIdx = (slideIdx + 1) % MicroscopeController.StandardSlides.Length;
                var sl = MicroscopeController.StandardSlides[slideIdx];
                if (hostMicroscope != null) hostMicroscope.LoadSlideById(sl.id);
                S.Done("kep_tieu_ban");
                S.Say($"Đã nạp tiêu bản: {sl.title}", 1);
            });

            bCoarse = S.AddButton("Ốc đại cấp (+0,1 mm)", 0, 1, () =>
            {
                if (hostMicroscope != null)
                {
                    float delta = hostMicroscope.currentObjectiveIndex >= 2 ? 0.25f : (Mathf.Abs(hostMicroscope.FocusOffsetMm) > 0.01f ? hostMicroscope.FocusOffsetMm : 0.05f);
                    hostMicroscope.AdjustCoarseFocus(delta);
                }
                S.Done("lay_net");
            });
            bFinePlus = S.AddButton("Ốc vi cấp (+2 µm)", 1, 1, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(2f);
                S.Done("lay_net");
            });
            bFineMinus = S.AddButton("Ốc vi cấp (-2 µm)", 2, 1, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(-2f);
                S.Done("lay_net");
            });

            bOil = S.AddButton("Nhỏ dầu soi 100×", 0, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.ApplyImmersionOil();
                S.Done("dau_soi_100x");
                S.Say("Đã nhỏ 1 giọt dầu soi immersion (n = 1,515) lên tiêu bản cho vật kính 100×.", 1);
            });
            bPlasmolysis = S.AddButton("NaCl 5% (Co NS)", 1, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.ApplyReagent("nacl");
                S.Say("Đã nhỏ NaCl 5%: Không bào co lại, màng sinh chất tách khỏi thành tế bào (co nguyên sinh).", 1);
            });
            bDeplasmolysis = S.AddButton("Nước cất (Phản co)", 2, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.ApplyReagent("nuoc_cat");
                S.Say("Đã nhỏ nước cất nhược trương: Không bào căng phồng trở lại (phản co nguyên sinh).", 1);
            });

            bStain = S.AddButton("Nhuộm màu tế bào", 0, 3, () =>
            {
                if (hostMicroscope != null)
                {
                    string nextStain = hostMicroscope.appliedStain == "lugol" ? "methylene_blue" : "lugol";
                    hostMicroscope.ApplyReagent(nextStain);
                }
                S.Say("Đã đổi chất nhuộm hiển vi (Lugol I-KI hoặc Xanh Methylene).", 1);
            });
            bMeasure = S.AddButton("Đo thước trắc vi", 1, 3, () =>
            {
                measuredLengthUm = hostMicroscope != null ? hostMicroscope.ComputeMeasuredCellSizeUm() : 220.0;
                double umPerDiv = hostMicroscope != null ? hostMicroscope.MicronsPerDivision : 10.0;
                markedCells++;
                S.Done("do_kich_thuoc");
                S.Say($"Đã đo bằng thước trắc vi (1 vạch = {BioFeedback.FormatVi(umPerDiv, "0.#")} µm): Kích thước tế bào = {BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm (đã đếm {markedCells} tế bào).", 1);
            }, true);
            bCapture = S.AddButton("Chiếu TV & Lưu PNG HD", 2, 3, () =>
            {
                if (hostMicroscope != null)
                {
                    hostMicroscope.cameraStreaming = true;
                    hostMicroscope.StreamToVideoWall();
                    lastSavedPng = hostMicroscope.CaptureMicrographPng();
                }
                else lastSavedPng = "hien_vi_bieu_bi_hanh_400x.png";
                S.Say($"Đã đồng bộ lên t2_tuong_video_2 & lưu ảnh HD: {lastSavedPng}", 1);
            }, true);

            bViewTab = S.AddButton("Chế độ: [Tiêu bản soi]", 0, 4, () =>
            {
                showStructureSchematic = !showStructureSchematic;
                Refresh();
            }, false, 3);

            statusText = S.AddText("", 5, 0, 3, 13);
            S.SetPlotAxes("THỊ TRƯỜNG QUANG HỌC & PHÂN BỐ KÍCH THƯỚC TẾ BÀO (µm)", "Vi trường thị kính / Chiều dài (µm)", "Cường độ / Số tế bào", "0", "400", "0", "20");
            Refresh();
        }

        public override void Refresh()
        {
            double mag = hostMicroscope != null ? hostMicroscope.CurrentMagnification : 400.0;
            double fov = hostMicroscope != null ? hostMicroscope.CurrentFovMm : 0.45;
            bool lamp = hostMicroscope == null || hostMicroscope.lampOn;
            bool oil = hostMicroscope != null && hostMicroscope.oilApplied;
            string slideCode = hostMicroscope != null ? hostMicroscope.activeSlideCode : "bieu_bi_hanh";

            InstrumentScreen.SetButton(bLamp, lamp ? "Đèn LED: BẬT" : "Đèn LED: TẮT", true, lamp);
            InstrumentScreen.SetButton(bOil, oil ? "Dầu soi 100×: ĐÃ NHỎ" : "Nhỏ dầu soi 100×", true, oil);
            InstrumentScreen.SetButton(bViewTab, showStructureSchematic ? "Chế độ xem: [Sơ đồ cấu tạo kính] ⇄ (Bấm đổi xem Tiêu bản)" : "Chế độ xem: [Tiêu bản soi] ⇄ (Bấm đổi xem Sơ đồ cấu tạo)", true, showStructureSchematic);

            if (statusText != null)
            {
                statusText.text = $"Phóng đại: {BioFeedback.FormatVi(mag, "0")}× · Thị trường: {BioFeedback.FormatVi(fov, "0.##")} mm · Mẫu: {slideCode} · Đã đếm: {markedCells} tế bào";
            }

            string stainInfo = hostMicroscope != null && !string.IsNullOrEmpty(hostMicroscope.appliedStain)
                ? hostMicroscope.appliedStain.ToUpper()
                : "Tự nhiên";
            string plasmolysisInfo = hostMicroscope != null && hostMicroscope.plasmolysisFactor > 0.05f
                ? $"Đang co nguyên sinh ({BioFeedback.FormatVi(hostMicroscope.plasmolysisFactor * 100f, "0")}%)"
                : "Bình thường";

            S.SetResults(
                $"<b>KẾT QUẢ CAMERA HIỂN VI KỸ THUẬT SỐ HD (1024PX)</b>\n" +
                $"• Độ phóng đại tổng: <b>{BioFeedback.FormatVi(mag, "0")}×</b> (Thị trường: {BioFeedback.FormatVi(fov, "0.##")} mm)\n" +
                $"• Tiêu bản đang soi: <b>{slideCode}</b> {(oil ? "(Đã nhỏ dầu soi n = 1,515)" : "")}\n" +
                $"• Kích thước tế bào đo trên thước trắc vi: <b>{BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm</b> · Đã đếm: <b>{markedCells}</b>\n" +
                $"• Trạng thái mẫu: <b>Nhuộm {stainInfo} · {plasmolysisInfo}</b>\n" +
                $"• Tệp ảnh đã lưu: <b>{(string.IsNullOrEmpty(lastSavedPng) ? "(Chưa chụp)" : lastSavedPng)}</b>");
        }

        public override void DrawSchematic(Khkt.Demo.Instruments.Painter p, float time)
        {
            p.Clear(new Color32(10, 18, 30, 255));
            // Vẽ đường đi tia sáng qua tụ quang -> tiêu bản -> vật kính -> thị kính & cảm biến CMOS
            p.FillRect(120, 40, 140, 24, new Color32(60, 85, 115, 255));   // Đèn LED đế
            p.FillRect(140, 95, 100, 18, new Color32(90, 130, 175, 255));  // Tụ quang Abbe
            p.FillRect(110, 145, 160, 8, new Color32(180, 230, 255, 255)); // Lam kính tiêu bản
            p.FillRect(155, 185, 70, 45, new Color32(120, 160, 210, 255)); // Vật kính
            p.FillRect(145, 265, 90, 50, new Color32(80, 195, 150, 255));  // Thị kính 10× / Camera
            if (hostMicroscope == null || hostMicroscope.lampOn)
                p.Line(190, 64, 190, 265, new Color32(255, 235, 120, 220), 3);
        }

        public override void DrawPlot(Khkt.Demo.Instruments.Painter p)
        {
            p.Clear(new Color32(8, 14, 24, 255));

            if (showStructureSchematic)
            {
                // SƠ ĐỒ CẤU TẠO KÍNH HIỂN VI QUANG HỌC OLYMPUS CX23 (SGK SINH HỌC 10)
                // 1. Chân đế & Bộ nguồn LED
                p.FillRect(60, 40, 280, 42, new Color32(220, 228, 238, 255));
                p.FillRect(50, 32, 300, 10, new Color32(40, 48, 60, 255)); // Đệm cao su chân đế
                p.FillRect(80, 50, 34, 22, new Color32(45, 55, 70, 255));  // Núm chiết áp sáng LED

                // 2. Tụ quang Abbe & Màn chắn sáng Iris
                p.FillRect(150, 95, 75, 34, new Color32(75, 98, 130, 255));
                p.FillRect(140, 122, 95, 8, new Color32(50, 65, 85, 255));

                // 3. Bàn sa trượt cơ học (Mechanical Stage)
                p.FillRect(100, 155, 180, 16, new Color32(32, 40, 52, 255));
                p.FillRect(125, 171, 55, 6, new Color32(190, 215, 235, 255)); // Kẹp giữ lam kính
                p.FillRect(255, 120, 18, 36, new Color32(55, 68, 85, 255));   // Ốc di chuyển sa trượt X-Y

                // 4. Đĩa xoay vật kính (Revolving Nosepiece)
                p.FillRect(145, 225, 90, 22, new Color32(50, 62, 78, 255));
                // 4 ống vật kính viền màu chuẩn quốc tế
                p.FillRect(148, 190, 16, 35, new Color32(185, 195, 208, 255)); // 4×
                p.FillRect(148, 196, 16, 4, new Color32(225, 45, 45, 255));    // Vạch đỏ 4×
                p.FillRect(168, 185, 16, 40, new Color32(185, 195, 208, 255)); // 10×
                p.FillRect(168, 192, 16, 4, new Color32(235, 205, 35, 255));   // Vạch vàng 10×
                p.FillRect(188, 180, 16, 45, new Color32(185, 195, 208, 255)); // 40×
                p.FillRect(188, 188, 16, 4, new Color32(40, 130, 235, 255));   // Vạch xanh lam 40×
                p.FillRect(208, 175, 16, 50, new Color32(185, 195, 208, 255)); // 100× dầu
                p.FillRect(208, 185, 16, 4, new Color32(245, 245, 250, 255));  // Vạch trắng 100×

                // 5. Thân máy cong công thái học CX23 & Cụm ốc điều chỉnh tiêu cự
                p.FillRect(275, 78, 50, 215, new Color32(220, 228, 238, 255));
                p.FillRect(290, 95, 44, 44, new Color32(50, 62, 78, 255));    // Ốc đại cấp (chỉnh thô)
                p.FillRect(322, 105, 24, 24, new Color32(85, 105, 130, 255)); // Ốc vi cấp (chỉnh tinh)

                // 6. Đầu quan sát hai mắt (Binocular Head nghiêng 30°)
                p.FillRect(165, 275, 130, 45, new Color32(220, 228, 238, 255));
                p.FillRect(145, 320, 24, 60, new Color32(38, 46, 58, 255)); // Thị kính trái 10×
                p.FillRect(185, 320, 24, 60, new Color32(38, 46, 58, 255)); // Thị kính phải 10×

                // Tia sáng quang học phát quang
                p.Line(187, 82, 187, 180, new Color32(255, 235, 120, 210), 3);

                // Thẻ chú thích SGK bên phải
                p.FillRect(410, 30, 480, 400, new Color32(14, 22, 34, 255));
                p.Frame(410, 30, 480, 400, new Color32(45, 160, 185, 200), 2);

                p.FillRect(425, 365, 450, 45, new Color32(22, 35, 52, 255));
                p.FillRect(425, 310, 450, 45, new Color32(18, 28, 42, 255));
                p.FillRect(425, 255, 450, 45, new Color32(22, 35, 52, 255));
                p.FillRect(425, 200, 450, 45, new Color32(18, 28, 42, 255));
                p.FillRect(425, 145, 450, 45, new Color32(22, 35, 52, 255));
                p.FillRect(425, 90, 450, 45, new Color32(18, 28, 42, 255));
                p.FillRect(425, 35, 450, 45, new Color32(22, 35, 52, 255));
                return;
            }

            // CHẾ ĐỘ XEM TIÊU BẢN THỊ KÍNH TRÒN SDF ANTI-ALIASING SIÊU MỊN
            int cx = 220, cy = 230, rMax = 180;
            float rOuter = rMax + 8f;
            float rOuter2 = rOuter * rOuter;
            string code = hostMicroscope != null ? hostMicroscope.activeSlideCode : "bieu_bi_hanh";
            float mag = hostMicroscope != null ? (float)(hostMicroscope.CurrentMagnification / 10.0) : 10f;
            float scale = Mathf.Max(0.4f, mag / 10f);
            float blur = hostMicroscope != null ? Mathf.Clamp((float)hostMicroscope.ComputeCurrentBlurFactor(), 0f, 4f) : 0f;
            bool stainedBlue = code.Contains("niem_mac") || (hostMicroscope?.slideSample != null && hostMicroscope.slideSample.stainId.Contains("methylen"));
            bool isStomata = code.Contains("khi_khong") || code.Contains("thai_lai");
            bool gram = code.Contains("gram");
            float plasmo = code.Contains("co_nguyen_sinh") ? 0.55f : (hostMicroscope?.mountedSlide != null ? hostMicroscope.mountedSlide.plasmolysisFraction : 0f);

            int yMin = Mathf.Max(0, cy - (int)rOuter);
            int yMax = Mathf.Min(p.H - 1, cy + (int)rOuter);

            for (int py = yMin; py <= yMax; py++)
            {
                float dy = py - cy;
                float dy2 = dy * dy;
                if (dy2 >= rOuter2) continue;

                float halfW = Mathf.Sqrt(rOuter2 - dy2);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - halfW));
                int x1 = Mathf.Min(p.W - 1, Mathf.CeilToInt(cx + halfW));
                float v = (dy / rMax) / scale + (hostMicroscope != null ? hostMicroscope.stageY * 0.04f : 0f);

                for (int px = x0; px <= x1; px++)
                {
                    float dx = px - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy2);
                    // Thuật toán Signed Distance Field (SDF) Anti-Aliasing viền vòng tròn
                    float innerMask = Mathf.Clamp01((rMax - dist) / 2.0f);
                    float bezelMask = Mathf.Clamp01((rOuter - dist) / 2.0f) * (1f - innerMask);

                    if (innerMask <= 0f && bezelMask <= 0f) continue;

                    Color32 bezelCol = new Color32(20, 24, 30, 255);
                    float hl = Mathf.Clamp01(1f - Mathf.Abs(dy - dx * 0.45f) / rMax) * 0.15f;
                    bezelCol = Color.Lerp(bezelCol, new Color(0.28f, 0.32f, 0.40f), hl);

                    if (innerMask <= 0f)
                    {
                        p.SetPixel(px, py, Color.Lerp(new Color32(8, 14, 24, 255), bezelCol, bezelMask));
                        continue;
                    }

                    float u = (dx / rMax) / scale + (hostMicroscope != null ? hostMicroscope.stageX * 0.04f : 0f);
                    Color c = MicroscopeController.SampleSpecimenColor(code, u, v, stainedBlue, isStomata, gram, gram, plasmo);
                    if (blur > 0.15f)
                        c = Color.Lerp(c, new Color(0.82f, 0.80f, 0.86f), Mathf.Clamp01(blur * 0.3f));

                    // Góc tối vignette tự nhiên quanh rìa thị kính quang học
                    float vig = Mathf.Clamp01(1f - Mathf.Pow(dist / rMax, 4.0f) * 0.35f);
                    c *= vig;

                    c = Color.Lerp(bezelCol, c, innerMask);
                    if (bezelMask > 0f)
                        c = Color.Lerp(new Color(0.03f, 0.05f, 0.09f), c, innerMask + bezelMask);

                    p.SetPixel(px, py, (Color32)c);
                }
            }

            // Thước trắc vi thị kính ngang tâm
            p.Line(cx - 130, cy, cx + 130, cy, new Color32(20, 25, 30, 210), 2);
            for (int k = -10; k <= 10; k++)
            {
                int tx = cx + k * 12;
                int tlen = (k % 5 == 0) ? 14 : 7;
                p.Line(tx, cy, tx, cy + tlen, new Color32(20, 25, 30, 210), 1);
            }

            // 2. Bên phải: Biểu đồ phân bố kích thước tế bào đo được
            int bars = 6;
            for (int i = 0; i < bars; i++)
            {
                int x = 450 + i * 62;
                float d = Mathf.Abs(i - 2.5f);
                int h = Mathf.RoundToInt(Mathf.Max(28f, 290f * Mathf.Exp(-0.4f * d * d)));
                p.FillRect(x, 40, 42, h, new Color32(75, 195, 145, 255));
                p.Frame(x, 40, 42, h, new Color32(110, 240, 185, 255), 2);
            }
        }
    }
}

