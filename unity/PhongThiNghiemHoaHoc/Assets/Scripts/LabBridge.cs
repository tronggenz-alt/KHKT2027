// LabBridge – GameObject DUY NHẤT trong cảnh. Tự dựng camera, đèn, bàn; nhận lệnh từ trang web và gửi thao tác về.
//   JS → Unity: unityInstance.SendMessage("LabBridge", "NhanLenh", '{"lenh":"…","du_lieu":"<chuỗi JSON>"}')
//     mo_thi_nghiem {id, ten, trang_thai:"<JSON>"} · trang_thai "<JSON>" · hieu_ung {loai, so, id} · doi_giao_dien {nen, nhan, the, chu, toi}
//     chat_luong {q} · camera {lenh}
//   Unity → JS: PTN_GuiSuKien(json) (Plugins/WebGL/LabBridge.jslib) → window.PhongLabUnity.nhan(json)
//     {su_kien: "san_sang"} · {su_kien:"thao_tac", lenh, tham_so:"<JSON>"} · {su_kien:"chon", loai, so} · {su_kien:"loi", thong_bao}
// Unity KHÔNG tự tính hoá học – mọi con số đến từ lõi mo-phong.js của trang web.
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhongThiNghiem
{
    public class LabBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PTN_GuiSuKien(string json);
#else
        static void PTN_GuiSuKien(string json) { Debug.Log("[LabBridge → web] " + json); }
#endif

        public Color MauNen = new Color(0.933f, 0.957f, 0.980f);
        public Color MauNhan = new Color(0.059f, 0.169f, 0.282f);
        public bool Toi;
        public bool ChatLuongCao = true;

        Camera _cam;
        LabCamera _dk;
        Light _den;
        Renderer _ban;
        Transform _goc;
        CanhCoSo _canh;

        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;   // để ô nhập trên trang web vẫn gõ được
#endif
            var cg = new GameObject("Camera");
            cg.tag = "MainCamera";
            _cam = cg.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = MauNen;
            _cam.fieldOfView = 38f;
            _cam.nearClipPlane = 0.01f;
            _cam.farClipPlane = 40f;
            _dk = cg.AddComponent<LabCamera>();
            // Toạ độ dụng cụ chép từ bản Web 3D (three.js – hệ tay phải); Unity là hệ tay trái → lật ngang phép chiếu để ảnh
            // không bị gương trái–phải, đồng thời đảo hướng cull khi vẽ camera này.
            RenderPipelineManager.beginCameraRendering += TruocKhiVe;
            RenderPipelineManager.endCameraRendering += SauKhiVe;

            var lg = new GameObject("Den");
            _den = lg.AddComponent<Light>();
            _den.type = LightType.Directional;
            _den.intensity = 1.3f;
            _den.shadows = LightShadows.Soft;
            lg.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.9f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.38f, 0.42f);

            var ban = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ban.name = "Ban";
            ban.transform.position = new Vector3(0f, -0.03f, 0f);
            ban.transform.localScale = new Vector3(2.6f, 0.06f, 1.25f);
            _ban = ban.GetComponent<Renderer>();
            _ban.material = LabVatLieu.Dac(new Color(0.88f, 0.91f, 0.95f), 0.12f);

            _goc = new GameObject("CanhHienTai").transform;
        }

        void Start() => Gui(new SuKienUnity { su_kien = "san_sang" });

        void TruocKhiVe(ScriptableRenderContext ctx, Camera c) { if (c == _cam) GL.invertCulling = true; }
        void SauKhiVe(ScriptableRenderContext ctx, Camera c) { if (c == _cam) GL.invertCulling = false; }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= TruocKhiVe;
            RenderPipelineManager.endCameraRendering -= SauKhiVe;
        }

        void LateUpdate()
        {
            // giữ phép chiếu lật ngang (cả khi khung đổi tỉ lệ) – raycast ScreenPointToRay dùng cùng ma trận này
            _cam.ResetProjectionMatrix();
            _cam.projectionMatrix = _cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        }

        public static void Gui(SuKienUnity e) => PTN_GuiSuKien(JsonUtility.ToJson(e));
        public static void GuiThaoTac(string lenh, string thamSo = "{}") => Gui(new SuKienUnity { su_kien = "thao_tac", lenh = lenh, tham_so = thamSo });

        /// <summary>Trang web gọi qua SendMessage("LabBridge", "NhanLenh", json).</summary>
        public void NhanLenh(string json)
        {
            try
            {
                var l = JsonUtility.FromJson<LenhJS>(json);
                switch (l.lenh)
                {
                    case "mo_thi_nghiem":
                        var mo = JsonUtility.FromJson<MoThiNghiemDL>(l.du_lieu);
                        MoCanh(mo.id, mo.trang_thai);
                        break;
                    case "trang_thai":
                        _canh?.CapNhat(l.du_lieu);
                        break;
                    case "hieu_ung":
                        _canh?.HieuUng(JsonUtility.FromJson<HieuUngDL>(l.du_lieu));
                        break;
                    case "doi_giao_dien":
                        DoiGiaoDien(JsonUtility.FromJson<GiaoDienDL>(l.du_lieu));
                        break;
                    case "chat_luong":
                        DatChatLuong(JsonUtility.FromJson<ChatLuongDL>(l.du_lieu).q);
                        break;
                    case "camera":
                        _dk.Lenh(JsonUtility.FromJson<CameraDL>(l.du_lieu).lenh);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Gui(new SuKienUnity { su_kien = "loi", thong_bao = ex.Message });
            }
        }

        void MoCanh(string id, string trangThai)
        {
            _canh?.Huy();
            for (int i = _goc.childCount - 1; i >= 0; i--) Destroy(_goc.GetChild(i).gameObject);
            _canh = id switch
            {
                "chuan-do" => new CanhChuanDo(),
                "an-toan" => new CanhAnToan(),
                "nhan-biet-ion" => new CanhNhanBiet(),
                "pin-galvani" => new CanhPin(),
                "dien-phan" => new CanhDienPhan(),
                _ => null,
            };
            if (_canh == null) return;
            _canh.Dung(_goc, this, trangThai);
            _dk.DatGoc(_canh.GocViTri, _canh.GocDich, _canh.GocGan, _canh.GocXa);
            _canh.CapNhat(trangThai);
        }

        void DoiGiaoDien(GiaoDienDL g)
        {
            Toi = g.toi;
            if (ColorUtility.TryParseHtmlString(g.nen, out var nen)) MauNen = nen;
            if (ColorUtility.TryParseHtmlString(g.nhan, out var nhan)) MauNhan = nhan;
            _cam.backgroundColor = MauNen;
            Color ban = Color.Lerp(MauNen, Toi ? Color.white : MauNhan, Toi ? 0.07f : 0.13f);
            _ban.material.SetColor("_BaseColor", ban);
            RenderSettings.ambientIntensity = Toi ? 0.8f : 1f;
            _canh?.DoiMau();
        }

        void DatChatLuong(string q)
        {
            ChatLuongCao = q == "cao";
            _den.shadows = ChatLuongCao ? LightShadows.Soft : LightShadows.None;
        }

        void Update()
        {
            _canh?.Tick(Time.deltaTime);
            if (Input.GetMouseButtonUp(0) && !_dk.VuaKeo)
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                {
                    var n = hit.collider.GetComponentInParent<LabNhap>();
                    if (n == null) return;
                    if (!string.IsNullOrEmpty(n.lenh)) GuiThaoTac(n.lenh, n.thamSo);
                    else if (!string.IsNullOrEmpty(n.chonLoai)) Gui(new SuKienUnity { su_kien = "chon", loai = n.chonLoai, so = n.chonSo });
                }
            }
        }
    }
}
