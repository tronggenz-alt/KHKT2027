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
        Button bLamp, bObj, bFinePlus, bFineMinus, bMeasure, bCount, bCapture, bStream;
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
            bFinePlus = S.AddButton("Lấy nét tinh (+2 µm)", 2, 0, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(2f);
                S.Done("lay_net");
            });
            bFineMinus = S.AddButton("Lấy nét tinh (-2 µm)", 0, 1, () =>
            {
                if (hostMicroscope != null) hostMicroscope.AdjustFineFocus(-2f);
                S.Done("lay_net");
            });
            bMeasure = S.AddButton("Đo kích thước tế bào", 1, 1, () =>
            {
                measuredLengthUm = hostMicroscope != null ? hostMicroscope.ComputeMeasuredCellSizeUm() : 220.0;
                S.Done("do_kich_thuoc");
                S.Say($"Đã đo bằng thước trắc vi thị kính: chiều dài tế bào = {BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm.", 1);
            }, true);
            bCount = S.AddButton("Đánh dấu đếm tế bào (+1)", 2, 1, () =>
            {
                markedCells++;
                S.Say($"Đã đánh dấu {markedCells} tế bào trong thị trường quan sát.", 0);
            });
            bStream = S.AddButton("Chiếu lên màn hình lớp", 0, 2, () =>
            {
                if (hostMicroscope != null) hostMicroscope.cameraStreaming = !hostMicroscope.cameraStreaming;
                S.Say("Đã đồng bộ luồng video HDMI lên màn hình tường video 75 inch.", 1);
            });
            bCapture = S.AddButton("Chụp & Lưu ảnh PNG", 1, 2, () =>
            {
                if (hostMicroscope != null) lastSavedPng = hostMicroscope.CaptureMicrographPng();
                else lastSavedPng = "hien_vi_bieu_bi_hanh_400x.png";
                S.Say($"Đã lưu ảnh tiêu bản: {lastSavedPng}", 1);
            }, true, 2);

            statusText = S.AddText("", 3, 0, 3, 13);
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

            S.SetResults(
                $"<b>KẾT QUẢ CAMERA HIỂN VI KỸ THUẬT SỐ</b>\n" +
                $"• Độ phóng đại tổng: <b>{BioFeedback.FormatVi(mag, "0")}×</b> (Thị trường: {BioFeedback.FormatVi(fov, "0.##")} mm)\n" +
                $"• Kích thước tế bào đo trên thước trắc vi: <b>{BioFeedback.FormatVi(measuredLengthUm, "0.0")} µm</b>\n" +
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

