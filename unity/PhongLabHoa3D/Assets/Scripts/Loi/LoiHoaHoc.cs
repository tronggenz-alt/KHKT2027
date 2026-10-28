#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Jint;
using UnityEngine;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.Loi
{
    public class LoiHoaHoc : ILoiHoaHoc
    {
        private Engine _engine;
        private Thread _workerThread;
        private volatile bool _dangChayLuong;
        private volatile bool _choPhepCapNhat; // Chạy thời gian khi đang tại bàn
        private readonly object _khoaEngine = new object();

        private string _thuMucPtn;
        private string _tinhToanJs;
        private string _moPhongJs;
        private string _bangIonJson;

        private volatile string _moPhongHienTaiId;
        private volatile string _trangThaiMoiNhatJson = "{}";
        private volatile string _soTayMoiNhatJson = "[]";

        private readonly ConcurrentQueue<ThaoTacYeuCau> _hangDoiThaoTac = new ConcurrentQueue<ThaoTacYeuCau>();
        private readonly ConcurrentQueue<SuKienHieuUng> _hangDoiHieuUng = new ConcurrentQueue<SuKienHieuUng>();
        private readonly ConcurrentQueue<string> _hangDoiTrangThai = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _hangDoiSoTay = new ConcurrentQueue<string>();

        public event Action<string> OnTrangThai;
        public event Action<string> OnSoTay;
        public event Action<SuKienHieuUng> OnHieuUng;

        public bool DangMoPhong => !string.IsNullOrEmpty(_moPhongHienTaiId);
        public string MoPhongId => _moPhongHienTaiId;

        public LoiHoaHoc(string thuMucPtn = null)
        {
            if (string.IsNullOrEmpty(thuMucPtn))
            {
                thuMucPtn = Path.Combine(Application.streamingAssetsPath, "ptn");
            }
            _thuMucPtn = thuMucPtn;
            NapFileNguon();
            KhoiTaoEngine();
            BatDauLuongMoPhong();
        }

        private void NapFileNguon()
        {
            string fileTt = Path.Combine(_thuMucPtn, "tinh-toan.js");
            string fileMp = Path.Combine(_thuMucPtn, "mo-phong.js");
            string fileIon = Path.Combine(_thuMucPtn, "bang-ion.json");

            if (!File.Exists(fileTt) || !File.Exists(fileMp) || !File.Exists(fileIon))
            {
                throw new FileNotFoundException($"[LoiHoaHoc] Thiếu file lõi trong: {_thuMucPtn}");
            }

            _tinhToanJs = File.ReadAllText(fileTt);
            _moPhongJs = File.ReadAllText(fileMp);
            _bangIonJson = File.ReadAllText(fileIon);
        }

        private void KhoiTaoEngine()
        {
            lock (_khoaEngine)
            {
                _engine = new Engine(cfg =>
                {
                    cfg.AllowClr();
                });

                // Cấu hình môi trường UMD giả lập browser
                _engine.Execute("var self = this; var window = this;");
                _engine.Execute(_tinhToanJs);
                _engine.Execute(_moPhongJs);

                // Đăng ký callback từ JS về C#
                _engine.SetValue("csGuiHieuUng", new Action<string, string>((ten, json) =>
                {
                    _hangDoiHieuUng.Enqueue(new SuKienHieuUng { Ten = ten, DuLieuJson = json });
                }));
                _engine.SetValue("csGuiTrangThai", new Action<string>((json) =>
                {
                    _trangThaiMoiNhatJson = json;
                    _hangDoiTrangThai.Enqueue(json);
                }));
                _engine.SetValue("csGuiSoTay", new Action<string>((json) =>
                {
                    _soTayMoiNhatJson = json;
                    _hangDoiSoTay.Enqueue(json);
                }));
            }
        }

        public void Tao(string id, string cheDo = "huong_dan")
        {
            string duLieuFile = Path.Combine(_thuMucPtn, "thi-nghiem", $"{id}.json");
            string duLieuJson = null;
            if (File.Exists(duLieuFile))
            {
                duLieuJson = File.ReadAllText(duLieuFile);
            }
            else
            {
                duLieuJson = DocTaiNguyen.DocChuoi($"ptn/thi-nghiem/{id}.json");
            }

            if (string.IsNullOrEmpty(duLieuJson))
            {
                Debug.LogError($"[LoiHoaHoc] Không tìm thấy dữ liệu thí nghiệm: {id}");
                return;
            }

            lock (_khoaEngine)
            {
                _moPhongHienTaiId = id;
                _engine.SetValue("rawDuLieu", duLieuJson);
                _engine.SetValue("rawBangIon", _bangIonJson);
                _engine.SetValue("rawCheDo", cheDo);

                _engine.Execute(@"
                    var duLieu = JSON.parse(rawDuLieu);
                    var bangIon = JSON.parse(rawBangIon);
                    var mp = self.PTNMoPhong.tao(duLieu, { bangIon: bangIon, cheDo: rawCheDo });
                    
                    mp.on('trang_thai', function(tt) {
                        csGuiTrangThai(JSON.stringify(tt));
                    });
                    mp.on('so_tay', function(st) {
                        csGuiSoTay(JSON.stringify(st));
                    });
                    mp.on('hieu_ung', function(e) {
                        csGuiHieuUng(typeof e === 'string' ? e : (e.loai || 'hieu_ung'), JSON.stringify(e));
                    });

                    csGuiTrangThai(JSON.stringify(mp.trangThai()));
                    csGuiSoTay(JSON.stringify(mp.soTay()));
                ");

                _choPhepCapNhat = true;
            }
        }

        public void ThaoTac(string lenh, string thamSoJson = null)
        {
            if (string.IsNullOrEmpty(lenh)) return;
            _hangDoiThaoTac.Enqueue(new ThaoTacYeuCau
            {
                Lenh = lenh,
                ThamSoJson = string.IsNullOrEmpty(thamSoJson) ? "{}" : thamSoJson
            });
        }

        public void ThaoTacDongBo(string lenh, string thamSoJson = null)
        {
            lock (_khoaEngine)
            {
                if (string.IsNullOrEmpty(_moPhongHienTaiId)) return;
                _engine.SetValue("tenLenh", lenh);
                _engine.SetValue("rawThamSo", string.IsNullOrEmpty(thamSoJson) ? "{}" : thamSoJson);
                _engine.Execute("mp.thaoTac(tenLenh, JSON.parse(rawThamSo));");
                _trangThaiMoiNhatJson = _engine.Evaluate("JSON.stringify(mp.trangThai())").AsString();
            }
        }

        public void BatDauMoPhong()
        {
            _choPhepCapNhat = true;
        }

        public void TamDungMoPhong()
        {
            _choPhepCapNhat = false;
        }

        public string TrangThai()
        {
            return _trangThaiMoiNhatJson;
        }

        public string SoTay()
        {
            return _soTayMoiNhatJson;
        }

        public void CapNhatDongBo(float dt)
        {
            lock (_khoaEngine)
            {
                if (string.IsNullOrEmpty(_moPhongHienTaiId)) return;

                while (_hangDoiThaoTac.TryDequeue(out var req))
                {
                    _engine.SetValue("tenLenh", req.Lenh);
                    _engine.SetValue("rawThamSo", req.ThamSoJson);
                    _engine.Execute("mp.thaoTac(tenLenh, JSON.parse(rawThamSo));");
                }

                _engine.SetValue("dt", (double)dt);
                _engine.Execute("mp.capNhat(dt);");
                _trangThaiMoiNhatJson = _engine.Evaluate("JSON.stringify(mp.trangThai())").AsString();
            }
        }

        public double TinhToan_pHChuanDo(double Ca, double Va, double Cb, double Vb, double? Ka = null)
        {
            lock (_khoaEngine)
            {
                _engine.SetValue("tCa", Ca);
                _engine.SetValue("tVa", Va);
                _engine.SetValue("tCb", Cb);
                _engine.SetValue("tVb", Vb);
                if (Ka.HasValue) _engine.SetValue("tKa", Ka.Value);
                else _engine.Execute("var tKa = null;");

                return _engine.Evaluate("self.PTNTinhToan.pHChuanDo({ Ca: tCa, Va: tVa, Cb: tCb, Vb: tVb, Ka: tKa })").AsNumber();
            }
        }

        public double TinhToan_DienPhanCu(double I, double t)
        {
            lock (_khoaEngine)
            {
                _engine.SetValue("tI", I);
                _engine.SetValue("tTime", t);
                return _engine.Evaluate("self.PTNTinhToan.dienPhan({ bai: 'cuso4_tro', I: tI, t: tTime, V_dd: 0.2, C0: 0.5 }).m_cathode").AsNumber();
            }
        }

        public double TinhToan_PinZnCu()
        {
            lock (_khoaEngine)
            {
                return _engine.Evaluate("self.PTNTinhToan.pinGalvani({ trai: 'Zn', phai: 'Cu' }).E0_pin").AsNumber();
            }
        }

        private void BatDauLuongMoPhong()
        {
            _dangChayLuong = true;
            _workerThread = new Thread(VongLapMoPhong)
            {
                Name = "LuongMoPhongLoiHoaHoc",
                IsBackground = true
            };
            _workerThread.Start();
        }

        private void VongLapMoPhong()
        {
            const int fpsMucTieu = 30;
            const int thoiGianNghiMs = 1000 / fpsMucTieu; // ~33ms
            const float dtGiay = 1.0f / fpsMucTieu;

            while (_dangChayLuong)
            {
                long thoiDiemBatDau = DateTime.UtcNow.Ticks;

                if (_choPhepCapNhat && !string.IsNullOrEmpty(_moPhongHienTaiId))
                {
                    lock (_khoaEngine)
                    {
                        // 1. Xử lý các lệnh thao tác trong hàng đợi
                        while (_hangDoiThaoTac.TryDequeue(out var req))
                        {
                            try
                            {
                                _engine.SetValue("tenLenh", req.Lenh);
                                _engine.SetValue("rawThamSo", req.ThamSoJson);
                                _engine.Execute("mp.thaoTac(tenLenh, JSON.parse(rawThamSo));");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[LoiHoaHoc] Lỗi thao tác {req.Lenh}: {ex.Message}");
                            }
                        }

                        // 2. Cập nhật thời gian mô phỏng
                        try
                        {
                            _engine.SetValue("dt", (double)dtGiay);
                            _engine.Execute("mp.capNhat(dt);");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[LoiHoaHoc] Lỗi cập nhật: {ex.Message}");
                        }
                    }
                }

                long daChayMs = (DateTime.UtcNow.Ticks - thoiDiemBatDau) / TimeSpan.TicksPerMillisecond;
                int canNghiMs = (int)Math.Max(1, thoiGianNghiMs - daChayMs);
                Thread.Sleep(canNghiMs);
            }
        }

        /// <summary>Gọi tại hàm Update() ở luồng chính Unity để phát các sự kiện</summary>
        public void CapNhatLuongChinh()
        {
            while (_hangDoiTrangThai.TryDequeue(out var tt))
            {
                OnTrangThai?.Invoke(tt);
            }

            while (_hangDoiSoTay.TryDequeue(out var st))
            {
                OnSoTay?.Invoke(st);
            }

            while (_hangDoiHieuUng.TryDequeue(out var e))
            {
                OnHieuUng?.Invoke(e);
            }
        }

        public void Dispose()
        {
            _dangChayLuong = false;
            _choPhepCapNhat = false;
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(500);
            }
            lock (_khoaEngine)
            {
                _engine = null;
            }
        }
    }
}
#endif

