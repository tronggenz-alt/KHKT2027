using System;
using System.Globalization;
using System.IO;
using System.Text;
using Khkt.Input;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Recording
{
    /// <summary>
    /// Ghi MỌI mẫu tay cầm (100 Hz) kèm trạng thái bài chuẩn độ ra CSV, để phân tích bằng
    /// khkt/skill.py (độ run tay, tốc độ 2 ml cuối, so với thao tác chuẩn của giáo viên).
    /// Cột đầu giống hệt tools/tay_cam_monitor.py.
    /// </summary>
    public class MotionRecorder : MonoBehaviour
    {
        public ControllerSource source;
        public BuretStopcock buret;
        public LabContainer flask;
        public SwirlDetector swirl;
        [Tooltip("Tên học sinh hoặc mã nhóm; giáo viên ghi thao tác chuẩn với tên 'chuan'")]
        public string sessionName = "hocsinh";
        public bool recordOnStart = true;

        public string CurrentPath { get; private set; }
        StreamWriter writer;
        readonly StringBuilder line = new StringBuilder(256);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Start()
        {
            if (recordOnStart) Begin();
        }

        public void Begin()
        {
            End();
            string dir = Path.Combine(Application.persistentDataPath, "sessions");
            Directory.CreateDirectory(dir);
            CurrentPath = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{sessionName}.csv");
            writer = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
            writer.WriteLine("t_ms,seq,ax,ay,az,gx,gy,gz,qw,qx,qy,qz,j1x,j1y,j2x,j2y,buttons,v_buret_ml,opening,ph,swirl,temp_c");
            source.SampleReceived += OnSample;
            Debug.Log($"[Ghi] {CurrentPath}");
        }

        public void End()
        {
            if (writer == null) return;
            source.SampleReceived -= OnSample;
            writer.Dispose();
            writer = null;
        }

        void OnDestroy() => End();

        void OnSample(ControllerState s)
        {
            var q = s.OrientationRH;
            line.Clear();
            line.Append(s.TimeMs).Append(',').Append(s.Seq);
            F(s.AccelG.x, "0.00000"); F(s.AccelG.y, "0.00000"); F(s.AccelG.z, "0.00000");
            F(s.GyroDps.x, "0.000"); F(s.GyroDps.y, "0.000"); F(s.GyroDps.z, "0.000");
            F(q.w, "0.000000"); F(q.x, "0.000000"); F(q.y, "0.000000"); F(q.z, "0.000000");
            F(s.Joy1.x, "0.000"); F(s.Joy1.y, "0.000"); F(s.Joy2.x, "0.000"); F(s.Joy2.y, "0.000");
            line.Append(',').Append((int)s.Buttons);
            F(buret != null ? buret.ReadingMl : float.NaN, "0.0000");
            F(buret != null ? buret.Opening : float.NaN, "0.0000");
            F(flask != null ? (float)flask.BulkPh : float.NaN, "0.000");
            F(swirl != null ? swirl.Intensity : float.NaN, "0.000");
            F(flask != null ? flask.TemperatureC : float.NaN, "0.00");
            writer.WriteLine(line);
        }

        void F(float v, string fmt) => line.Append(',').Append(v.ToString(fmt, Inv));
    }
}
