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
        Button bLamp, bObj, bSlide, bCoarse, bFinePlus, bFineMinus, bOil, bPlasmolysis, bDeplasmolysis, bStain, bMeasure, bCapture;
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

            statusText = S.AddText("", 4, 0, 3, 13);
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
            // 1. Bên trái: Khung ngắm thị kính tròn trực tiếp (Circular Eyepiece Viewport)
            int cx = 210, cy = 200, rMax = 150;
            string code = hostMicroscope != null ? hostMicroscope.activeSlideCode : "bieu_bi_hanh";
            float mag = hostMicroscope != null ? (float)(hostMicroscope.CurrentMagnification / 10.0) : 10f;
            float scale = Mathf.Max(0.4f, mag / 10f);
            float blur = hostMicroscope != null ? Mathf.Clamp((float)hostMicroscope.ComputeCurrentBlurFactor(), 0f, 4f) : 0f;
            bool stainedBlue = code.Contains("niem_mac") || (hostMicroscope?.slideSample != null && hostMicroscope.slideSample.stainId.Contains("methylen"));
            bool isStomata = code.Contains("khi_khong") || code.Contains("thai_lai");
            bool gram = code.Contains("gram");
            float plasmo = code.Contains("co_nguyen_sinh") ? 0.55f : (hostMicroscope?.mountedSlide != null ? hostMicroscope.mountedSlide.plasmolysisFraction : 0f);

            int step = 4;
            for (int dy = -rMax; dy <= rMax; dy += step)
            {
                for (int dx = -rMax; dx <= rMax; dx += step)
                {
                    if (dx * dx + dy * dy > rMax * rMax) continue;
                    float u = ((float)dx / rMax) / scale + (hostMicroscope != null ? hostMicroscope.stageX * 0.04f : 0f);
                    float v = ((float)dy / rMax) / scale + (hostMicroscope != null ? hostMicroscope.stageY * 0.04f : 0f);
                    Color c = MicroscopeController.SampleSpecimenColor(code, u, v, stainedBlue, isStomata, gram, gram, plasmo);
                    if (blur > 0.15f)
                        c = Color.Lerp(c, new Color(0.82f, 0.80f, 0.86f), Mathf.Clamp01(blur * 0.3f));
                    p.FillRect(cx + dx, cy + dy, step, step, (Color32)c);
                }
            }
            // Thước trắc vi thị kính ngang tâm
            p.Line(cx - 110, cy, cx + 110, cy, new Color32(20, 25, 30, 210), 2);

            // 2. Bên phải: Biểu đồ phân bố kích thước tế bào đo được
            int bars = 6;
            for (int i = 0; i < bars; i++)
            {
                int x = 420 + i * 58;
                float d = Mathf.Abs(i - 2.5f);
                int h = Mathf.RoundToInt(Mathf.Max(25f, 280f * Mathf.Exp(-0.4f * d * d)));
                p.FillRect(x, 40, 38, h, new Color32(75, 195, 145, 255));
            }
        }
    }
}

