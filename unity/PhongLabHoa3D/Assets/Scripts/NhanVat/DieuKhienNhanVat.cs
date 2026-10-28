using System;
using UnityEngine;
using PhongThiNghiem;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.GiaoDien;

namespace PhongThiNghiem.NhanVat
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(DungNhanVat))]
    [ExecuteAlways]
    public class DieuKhienNhanVat : MonoBehaviour
    {
        public static DieuKhienNhanVat Instance { get; private set; }

        public enum CheDoCamera { NgoiThuBa, NgoiThuNhat, ToanCanhNhaBupBe, CanCanhBan }

        [Header("Tốc độ di chuyển")]
        public float TocDoDiThuong = 3.0f;
        public float TocDoChayNhanh = 5.2f;
        public float TocDoDiCham = 1.6f;
        public float LucNhay = 4.5f;
        public float TrongLuc = 18.0f;

        [Header("Camera & Góc nhìn")]
        public Camera CameraChinh;
        public CheDoCamera CheDoCam = CheDoCamera.NgoiThuBa;
        public float DoNhayChuot = 2.0f;
        public float CuLyTayDon = 3.2f;
        public float CuLyToiThieu = 1.2f;
        public float CuLyToiDa = 5.5f;

        [Header("Trạng thái")]
        public bool TaiBan;
        public string TramHienTai;
        public string KhuHienTai;
        public string TramGanNhatId;

        public event Action<string> OnQuaRanhGioiKhu;
        public event Action<string> OnGanTram; // Gợi ý [E] Vào bàn ...
        public event Action<bool> OnTrangThaiTaiBan;

        private CharacterController _cc;
        private DungNhanVat _hinh;
        private float _yaw = 180f; // Bắt đầu nhìn về hướng Bắc (-Z)
        private float _pitch = 10f;
        private float _vanTocY;
        private float _demThoiGianDungGanBan;
        private float _cooldownTuVaoBan;
        private Vector3 _viTriCamHienTai;
        private Quaternion _quayCamHienTai;

        public void DatHuongNhin(float pitch, float yaw)
        {
            _pitch = pitch;
            _yaw = yaw;
        }

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _hinh = GetComponent<DungNhanVat>();
            _cc.radius = 0.25f;
            _cc.height = 1.7f;
            _cc.center = new Vector3(0f, 0.85f, 0f);
        }

        void Start()
        {
            if (CameraChinh == null) CameraChinh = Camera.main;
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var t = System.Type.GetType("UnityEngine.WebGLInput, UnityEngine.WebGLModule");
                if (t != null)
                {
                    var p = t.GetProperty("captureAllKeyboardInput");
                    p?.SetValue(null, false);
                }
            }
            catch {}
            KhoaChuot(false);
#else
            KhoaChuot(true);
#endif
            DatCuChi(SuKienLab.MoApp);
        }

        public void KhoaChuot(bool khoa)
        {
            Cursor.lockState = khoa ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !khoa;
        }

        void Update()
        {
            // Phím Alt để tạm thời nhả chuột
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || TaiBan)
            {
                if (Cursor.lockState != CursorLockMode.None) KhoaChuot(false);
            }
            else
            {
                if (Cursor.lockState == CursorLockMode.None && Input.GetMouseButtonDown(0))
                {
                    // Trên WebGL: chỉ khoá chuột nếu không mở Menu
                    if (GiaoDienApp.Instance == null || (!GiaoDienApp.Instance.MenuChinhDangMo && !GiaoDienApp.Instance.CaiDatDangMo))
                    {
                        KhoaChuot(true);
                    }
                }
            }

            if (_cooldownTuVaoBan > 0f) _cooldownTuVaoBan -= Time.deltaTime;

            if (!TaiBan)
            {
                XuLyXoayCameraChuot();
                XuLyDiChuyen();
                XuLyChuyenCheDoCamera();
                KiemTraKhuVucVaTramGanNhat();
            }
            else
            {
                // Khi đang tại bàn: Phím Q để rời bàn
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    RoiBan();
                }
            }

            CapNhatViTriCamera(Time.deltaTime);
        }

        private void XuLyXoayCameraChuot()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mx = Input.GetAxis("Mouse X") * DoNhayChuot;
            float my = Input.GetAxis("Mouse Y") * DoNhayChuot;

            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, -35f, 65f);

            // Thu phóng tay đòn camera
            float cuon = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(cuon) > 0.01f)
            {
                CuLyTayDon = Mathf.Clamp(CuLyTayDon - cuon * 2.5f, CuLyToiThieu, CuLyToiDa);
            }
        }

        private void XuLyDiChuyen()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 huongCamNgang = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 huongNgangPhai = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 huongDi = (huongCamNgang * v + huongNgangPhai * h).normalized;

            // Xác định tốc độ
            float tocDoMucTieu = TocDoDiThuong;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) tocDoMucTieu = TocDoChayNhanh;
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) tocDoMucTieu = TocDoDiCham;

            Vector3 diChuyen = huongDi * tocDoMucTieu;

            // Trọng lực và nhảy
            if (_cc.isGrounded)
            {
                if (_vanTocY < 0f) _vanTocY = -2f;
                if (Input.GetButtonDown("Jump"))
                {
                    _vanTocY = LucNhay;
                    DatCuChi(SuKienLab.Nhay);
                }
            }
            else
            {
                _vanTocY -= TrongLuc * Time.deltaTime;
            }
            diChuyen.y = _vanTocY;

            _cc.Move(diChuyen * Time.deltaTime);

            // Quay mặt nhân vật mượt theo hướng di chuyển
            if (huongDi.sqrMagnitude > 0.01f)
            {
                Quaternion huongQuayDich = Quaternion.LookRotation(huongDi, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, huongQuayDich, Time.deltaTime * 12f);
            }

            // Hoạt hình chân tay
            float tocDoMatDat = new Vector2(diChuyen.x, diChuyen.z).magnitude;
            _hinh.CapNhatHoatHinh(tocDoMatDat, _cc.isGrounded, Time.deltaTime);

            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat("Speed", tocDoMatDat);
                anim.SetBool("IsGrounded", _cc.isGrounded);
                anim.SetFloat("VerticalVelocity", _vanTocY);
            }
        }

        public void DatCuChi(SuKienLab sk)
        {
            string clip = BangTrangThaiNhanVat.AnhXa(sk);
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.CrossFade(clip, 0.2f);
            }
            _hinh?.ThucHienCuChi(sk);
        }

        public void DatCheDoCamera(CheDoCamera cheDo, bool ngayLapTuc = false)
        {
            CheDoCam = cheDo;
            if (cheDo == CheDoCamera.ToanCanhNhaBupBe)
            {
                DungPhongLab.Instance?.BatTatTranNha(false);
                _hinh?.SetHienThi(false);
            }
            else
            {
                DungPhongLab.Instance?.BatTatTranNha(true);
                if (cheDo == CheDoCamera.CanCanhBan)
                {
                    _hinh?.SetHienThi(false);
                }
                else if (cheDo == CheDoCamera.NgoiThuNhat)
                {
                    _hinh?.SetFirstPerson(true);
                    if (_hinh?.Dau != null) _hinh.Dau.localScale = Vector3.zero;
                    _hinh?.SetHienThi(false);
                }
                else
                {
                    _hinh?.SetFirstPerson(false);
                    if (_hinh?.Dau != null) _hinh.Dau.localScale = Vector3.one;
                    _hinh?.SetHienThi(true);
                }
            }

            if (ngayLapTuc)
            {
                _viTriCamHienTai = Vector3.zero;
                CapNhatViTriCamera(100f);
            }
        }

        private void XuLyChuyenCheDoCamera()
        {
            // F: Ngôi 1 ⇄ Ngôi 3
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (CheDoCam == CheDoCamera.NgoiThuBa) DatCheDoCamera(CheDoCamera.NgoiThuNhat);
                else DatCheDoCamera(CheDoCamera.NgoiThuBa);
            }

            // V: Toàn cảnh nhà búp bê ⇄ Ngôi 3
            if (Input.GetKeyDown(KeyCode.V))
            {
                if (CheDoCam == CheDoCamera.ToanCanhNhaBupBe) DatCheDoCamera(CheDoCamera.NgoiThuBa);
                else DatCheDoCamera(CheDoCamera.ToanCanhNhaBupBe);
            }

            // E: Tương tác tủ đồ bảo hộ hoặc vào bàn
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!string.IsNullOrEmpty(TramGanNhatId))
                {
                    VaoBan(TramGanNhatId);
                }
                else
                {
                    // Kiểm tra gần tủ bảo hộ
                    float dTu = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(8.2f, 15.5f));
                    if (dTu < 1.8f)
                    {
                        _hinh.DeoDoBaoHo();
                    }
                }
            }
        }

        private void KiemTraKhuVucVaTramGanNhat()
        {
            Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);

            // 1. Kiểm tra khu vực
            if (DungToaNha.Instance?.DuLieu?.phong != null)
            {
                foreach (var p in DungToaNha.Instance.DuLieu.phong)
                {
                    if (p.hcn != null && p.hcn.Length >= 4)
                    {
                        if (pos2D.x >= p.hcn[0] && pos2D.x <= p.hcn[2] && pos2D.y >= p.hcn[1] && pos2D.y <= p.hcn[3])
                        {
                            if (KhuHienTai != p.id)
                            {
                                KhuHienTai = p.id;
                                OnQuaRanhGioiKhu?.Invoke(p.ten);
                            }
                            break;
                        }
                    }
                }
            }
            else if (DungPhongLab.Instance?.DuLieu?.khu != null)
            {
                foreach (var k in DungPhongLab.Instance.DuLieu.khu)
                {
                    if (k.hcn != null && k.hcn.Length >= 4)
                    {
                        if (pos2D.x >= k.hcn[0] && pos2D.x <= k.hcn[2] && pos2D.y >= k.hcn[1] && pos2D.y <= k.hcn[3])
                        {
                            if (KhuHienTai != k.id)
                            {
                                KhuHienTai = k.id;
                                OnQuaRanhGioiKhu?.Invoke(k.ten);
                            }
                            break;
                        }
                    }
                }
            }

            // 2. Kiểm tra trạm gần nhất
            string tramGan = null;
            float kcMin = float.MaxValue;

            if (DungToaNha.Instance?.DuLieu?.tram != null)
            {
                foreach (var tr in DungToaNha.Instance.DuLieu.tram)
                {
                    Vector2 dd = new Vector2(tr.diem_dung[0], tr.diem_dung[1]);
                    float d = Vector2.Distance(pos2D, dd);
                    float r = 2.4f;
                    if (d < r && d < kcMin)
                    {
                        kcMin = d;
                        tramGan = tr.id;
                    }
                }
            }
            else if (DungPhongLab.Instance?.DuLieu?.tram != null)
            {
                foreach (var tr in DungPhongLab.Instance.DuLieu.tram)
                {
                    Vector2 dd = new Vector2(tr.diem_dung[0], tr.diem_dung[1]);
                    float d = Vector2.Distance(pos2D, dd);
                    if (d < tr.ban_kinh_tuong_tac && d < kcMin)
                    {
                        kcMin = d;
                        tramGan = tr.id;
                    }
                }
            }

            if (tramGan != TramGanNhatId)
            {
                TramGanNhatId = tramGan;
                _demThoiGianDungGanBan = 0f;
                OnGanTram?.Invoke(tramGan);
            }

            // Đứng trước bàn 0.6s tự vào bàn (nếu không trong thời gian cooldown)
            if (!string.IsNullOrEmpty(TramGanNhatId) && _cooldownTuVaoBan <= 0f)
            {
                _demThoiGianDungGanBan += Time.deltaTime;
                if (_demThoiGianDungGanBan >= 0.6f)
                {
                    VaoBan(TramGanNhatId);
                }
            }
        }

        public void VaoBan(string tramId)
        {
            TaiBan = true;
            TramHienTai = tramId;
            DatCheDoCamera(CheDoCamera.CanCanhBan, true);
            KhoaChuot(false);

            // Dịch chuyển nhân vật vào đúng điểm dừng quay mặt vào bàn
            bool daDat = false;
            if (DungToaNha.Instance?.DuLieu?.tram != null)
            {
                var tr = DungToaNha.Instance.DuLieu.tram.Find(t => t.id == tramId || (tramId == "nhan-biet-ion" && t.id == "nhan-biet") || (tramId == "nhan-biet" && t.id == "nhan-biet-ion"));
                if (tr != null)
                {
                    transform.position = new Vector3(tr.diem_dung[0], 0.05f, tr.diem_dung[1]);
                    Vector3 huongNhinBan = new Vector3(tr.tam[0] - tr.diem_dung[0], 0f, tr.tam[1] - tr.diem_dung[1]).normalized;
                    transform.rotation = Quaternion.LookRotation(huongNhinBan, Vector3.up);
                    daDat = true;
                }
            }
            if (!daDat && DungPhongLab.Instance?.DuLieu?.tram != null)
            {
                var tr = DungPhongLab.Instance.DuLieu.tram.Find(t => t.id == tramId || (tramId == "nhan-biet-ion" && t.id == "nhan-biet") || (tramId == "nhan-biet" && t.id == "nhan-biet-ion"));
                if (tr != null)
                {
                    transform.position = new Vector3(tr.diem_dung[0], 0.05f, tr.diem_dung[1]);
                    Vector3 huongNhinBan = new Vector3(tr.tam[0] - tr.diem_dung[0], 0f, tr.tam[1] - tr.diem_dung[1]).normalized;
                    transform.rotation = Quaternion.LookRotation(huongNhinBan, Vector3.up);
                }
            }

            QuanLyTram.Instance?.ChuyenTram(tramId);
            DatCuChi(SuKienLab.VaoBanThiNghiem);
            OnTrangThaiTaiBan?.Invoke(true);
        }

        public void RoiBan()
        {
            TaiBan = false;
            DatCheDoCamera(CheDoCamera.NgoiThuBa, false);
            _cooldownTuVaoBan = 3.5f;
            _demThoiGianDungGanBan = 0f;

            // Lùi nhẹ 0.6m
            _cc.Move(-transform.forward * 0.6f);

            KhoaChuot(true);
            QuanLyTram.Instance?.RoiTram();
            OnTrangThaiTaiBan?.Invoke(false);
        }

        private void CapNhatViTriCamera(float dt)
        {
            if (CameraChinh == null) return;

            Vector3 viTriDich = Vector3.zero;
            Quaternion quayDich = Quaternion.identity;

            switch (CheDoCam)
            {
                case CheDoCamera.NgoiThuNhat:
                    viTriDich = transform.position + Vector3.up * 1.62f;
                    quayDich = Quaternion.Euler(_pitch, _yaw, 0f);
                    break;

                case CheDoCamera.ToanCanhNhaBupBe:
                    if (DungToaNha.Instance != null)
                    {
                        viTriDich = new Vector3(18.0f, 32.0f, -4.0f);
                        quayDich = Quaternion.Euler(60f, 0f, 0f);
                    }
                    else
                    {
                        viTriDich = new Vector3(11f, 16.0f, 19.0f);
                        quayDich = Quaternion.Euler(58f, 180f, 0f);
                    }
                    break;

                case CheDoCamera.CanCanhBan:
                    Vector3 tamBan = Vector3.zero;
                    Vector3 diemDung = Vector3.zero;
                    bool coTram = false;

                    if (DungToaNha.Instance?.DuLieu?.tram != null)
                    {
                        var tr = DungToaNha.Instance.DuLieu.tram.Find(t => t.id == TramHienTai || (TramHienTai == "nhan-biet-ion" && t.id == "nhan-biet") || (TramHienTai == "nhan-biet" && t.id == "nhan-biet-ion"));
                        if (tr != null)
                        {
                            tamBan = new Vector3(tr.tam[0], 0.95f, tr.tam[1]);
                            diemDung = new Vector3(tr.diem_dung[0], 0.95f, tr.diem_dung[1]);
                            coTram = true;
                        }
                    }
                    if (!coTram && DungPhongLab.Instance?.DuLieu?.tram != null)
                    {
                        var tr = DungPhongLab.Instance.DuLieu.tram.Find(t => t.id == TramHienTai || (TramHienTai == "nhan-biet-ion" && t.id == "nhan-biet") || (TramHienTai == "nhan-biet" && t.id == "nhan-biet-ion"));
                        if (tr != null)
                        {
                            tamBan = new Vector3(tr.tam[0], 0.95f, tr.tam[1]);
                            diemDung = new Vector3(tr.diem_dung[0], 0.95f, tr.diem_dung[1]);
                            coTram = true;
                        }
                    }

                    if (coTram)
                    {
                        // Cận cảnh bàn: nhìn trực diện dụng cụ thí nghiệm, hơi lệch trái để chừa 380px cho bảng điều khiển
                        Vector3 huongTuNguoi = (tamBan - diemDung).normalized;
                        Vector3 benPhai = Vector3.Cross(Vector3.up, huongTuNguoi);

                        viTriDich = new Vector3(diemDung.x, 1.35f, diemDung.z) + huongTuNguoi * 0.45f - benPhai * 0.12f;
                        quayDich = Quaternion.LookRotation((tamBan + Vector3.up * 0.12f) - viTriDich, Vector3.up);
                    }
                    else
                    {
                        viTriDich = transform.position - transform.forward * 1.5f + Vector3.up * 1.4f;
                        quayDich = transform.rotation;
                    }
                    break;

                case CheDoCamera.NgoiThuBa:
                default:
                    // Cách 3.6m, cao ngang vai, lệch phải 0.40m, FOV 60. Nhân vật chiếm ~25-35% chiều cao khung, không che giữa màn hình
                    Vector3 gocNhin = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back;
                    Vector3 vaiPhai = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right * 0.40f;
                    Vector3 diemGoc = transform.position + Vector3.up * 1.50f + vaiPhai;

                    // Raycast chống xuyên tường (chỉ va vào tường, bỏ qua layer 2 Ignore Raycast)
                    float tayDonThucTe = Mathf.Clamp(CuLyTayDon, 3.2f, 4.5f);
                    int maskKhongVaNguoi = ~LayerMask.GetMask("Ignore Raycast");
                    if (Physics.Raycast(diemGoc, gocNhin, out RaycastHit hitWall, tayDonThucTe, maskKhongVaNguoi, QueryTriggerInteraction.Ignore))
                    {
                        tayDonThucTe = Mathf.Max(1.6f, hitWall.distance - 0.25f);
                    }

                    viTriDich = diemGoc + gocNhin * tayDonThucTe;
                    quayDich = Quaternion.LookRotation((transform.position + Vector3.up * 1.30f + vaiPhai * 0.25f) - viTriDich, Vector3.up);
                    break;
            }

            if (_viTriCamHienTai == Vector3.zero)
            {
                _viTriCamHienTai = viTriDich;
                _quayCamHienTai = quayDich;
            }
            else
            {
                float tocDoBlend = CheDoCam == CheDoCamera.CanCanhBan ? 5.0f : 12.0f;
                _viTriCamHienTai = Vector3.Lerp(_viTriCamHienTai, viTriDich, dt * tocDoBlend);
                _quayCamHienTai = Quaternion.Slerp(_quayCamHienTai, quayDich, dt * tocDoBlend);
            }

            CameraChinh.transform.position = _viTriCamHienTai;
            CameraChinh.transform.rotation = _quayCamHienTai;
        }
    }
}
