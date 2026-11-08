using System.Collections.Generic;
using Khkt.Demo.Instruments;
using Khkt.Interaction.Bio;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Phần mềm vận hành Kính hiển vi kỹ thuật số giáo viên (5.1.2 - kinh_hien_vi_camera).
    /// Cho phép truyền hình ảnh trực tiếp lên màn hình lớn 75 inch, đo kích thước tế bào bằng thước micromet,
    /// đếm tế bào trong vi trường và chụp ảnh PNG lưu kết quả thí nghiệm.
    /// </summary>
    public class MicroscopeCameraProgram : Khkt.Demo.Instruments.InstrumentProgram
    {
        public override string Code => "kinh_hien_vi";
        readonly MicroscopeController hostMicroscope;

        int markedCells = 0;
        double measuredLengthUm = 225.0;
        string lastSavedPng = "";
        Button bLamp, bObj, bFinePlus, bFineMinus, bMeasure, bCount, bCapture, bStream, bPlasmolysis, bDeplasmolysis, bStain;
        Text statusText;

        public MicroscopeCameraProgram(MicroscopeController mic = null)
        {
            hostMicroscope = mic;
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
            });
            bStream = S.AddButton("Chiếu Màn hình Lớp", 2, 0, () =>
            {
                if (hostMicroscope != null)
                {
                    hostMicroscope.cameraStreaming = !hostMicroscope.cameraStreaming;
                    hostMicroscope.StreamToVideoWall();
                }
                S.Say("Đã đồng bộ luồng video HD lên màn hình tường video 75 inch (t2_tuong_video_2).", 1);
            });

            bFinePlus = S.AddButton("Ốc vi cấp (+2 µm)", 0, 1, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(2f);
                S.Done("lay_net");
            });
            bFineMinus = S.AddButton("Ốc vi cấp (-2 µm)", 1, 1, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(-2f);
                S.Done("lay_net");
            });
            bMeasure = S.AddButton("Đo thước trắc vi", 2, 1, () =>
            {
                measuredLengthUm = hostMicroscope != null ? hostMicroscope.ComputeMeasuredCellSizeUm() : 220.0;
                double umPerDiv = hostMicroscope != null ? hostMicroscope.MicronsPerDivision : 10.0;
                S.Done("do_kich_thuoc");
                S.Say($"Đã đo bằng thước trắc vi (1 vạch = {BioFeedback.FormatVi(umPerDiv, "0.#")} µm): Kích thước tế bào = {BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm.", 1);
            }, true);

            bPlasmolysis = S.AddButton("NaCl 5% (Co NS)", 0, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.ApplyReagent("nacl");
                S.Say("Đã nhỏ NaCl 5%: Không bào co lại, màng sinh chất tách khỏi thành tế bào (co nguyên sinh).", 1);
            });
            bDeplasmolysis = S.AddButton("Nước cất (Phản co)", 1, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.ApplyReagent("nuoc_cat");
                S.Say("Đã nhỏ nước cất nhược trương: Không bào căng phồng trở lại (phản co nguyên sinh).", 1);
            });
            bStain = S.AddButton("Nhuộm màu tế bào", 2, 2, () =>
            {
                if (hostMicroscope != null)
                {
                    string nextStain = hostMicroscope.appliedStain == "lugol" ? "methylene_blue" : "lugol";
                    hostMicroscope.ApplyReagent(nextStain);
                }
                S.Say("Đã đổi chất nhuộm hiển vi (Lugol I-KI hoặc Xanh Methylene).", 1);
            });

            bCount = S.AddButton("Đếm tế bào (+1)", 0, 3, () =>
            {
                markedCells++;
                S.Say($"Đã đánh dấu {markedCells} tế bào trong vi trường.", 0);
            });
            bCapture = S.AddButton("Chụp & Lưu ảnh HD 1024px", 1, 3, () =>
            {
                if (hostMicroscope != null) lastSavedPng = hostMicroscope.CaptureMicrographPng();
                else lastSavedPng = "hien_vi_bieu_bi_hanh_400x.png";
                S.Say($"Đã lưu ảnh tiêu bản HD: {lastSavedPng}", 1);
            }, true, 2);

            statusText = S.AddText("", 4, 0, 3, 13);
            S.SetPlotAxes("PHÂN BỐ KÍCH THƯỚC TẾ BÀO QUAN SÁT (µm)", "Chiều dài (µm)", "Số tế bào", "100", "400", "0", "20");
            Refresh();
        }

        public override void Refresh()
        {
            double mag = hostMicroscope != null ? hostMicroscope.CurrentMagnification : 400.0;
            double fov = hostMicroscope != null ? hostMicroscope.CurrentFovMm : 0.45;
            bool lamp = hostMicroscope == null || hostMicroscope.lampOn;
            bool stream = hostMicroscope == null || hostMicroscope.cameraStreaming;

            InstrumentScreen.SetButton(bLamp, lamp ? "Đèn LED: BẬT" : "Đèn LED: TẮT", true, lamp);
            InstrumentScreen.SetButton(bStream, stream ? "HDMI Lớp: ĐANG CHIẾU" : "HDMI Lớp: TẮT", true, stream);

            if (statusText != null)
            {
                statusText.text = $"Độ phóng đại: {BioFeedback.FormatVi(mag, "0")}× · Thị trường: {BioFeedback.FormatVi(fov, "0.##")} mm · Đã đếm: {markedCells} tế bào";
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
                $"• Kích thước tế bào đo trên thước trắc vi: <b>{BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm</b>\n" +
                $"• Trạng thái mẫu: <b>Nhuộm {stainInfo} · {plasmolysisInfo}</b>\n" +
                $"• Số tế bào đã đánh dấu trong vi trường: <b>{markedCells}</b>\n" +
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
            p.FillRect(145, 265, 90, 50, new Color32(80, 195, 150, 255));  // Cảm biến Camera 4K
            p.Line(190, 64, 190, 265, new Color32(255, 235, 120, 220), 3);
        }

        public override void DrawPlot(Khkt.Demo.Instruments.Painter p)
        {
            p.Clear(new Color32(8, 14, 24, 255));
            // Vẽ biểu đồ phân bố chiều dài tế bào biểu bì hành quanh giá trị đo được
            int bars = 8;
            for (int i = 0; i < bars; i++)
            {
                float x = 80 + i * 90;
                float d = Mathf.Abs(i - 3.5f);
                float h = Mathf.Max(25f, 320f * Mathf.Exp(-0.35f * d * d));
                p.FillRect((int)x, 40, 56, (int)h, new Color32(75, 195, 145, 255));
            }
        }
    }
}

