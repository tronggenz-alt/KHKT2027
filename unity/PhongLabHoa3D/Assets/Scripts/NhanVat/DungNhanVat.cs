using UnityEngine;
using PhongThiNghiem;

namespace PhongThiNghiem.NhanVat
{
    [ExecuteAlways]
    public class DungNhanVat : MonoBehaviour
    {
        public Transform Than { get; private set; }
        public Transform Dau { get; private set; }
        public Transform CanhTayTrai { get; private set; }
        public Transform CanhTayPhai { get; private set; }
        public Transform DuiTrai { get; private set; }
        public Transform DuiPhai { get; private set; }
        public GameObject KinhBaoHo { get; private set; }
        public GameObject GangTayTrai { get; private set; }
        public GameObject GangTayPhai { get; private set; }

        public bool DaDeoBaoHo { get; private set; }

        private float _phaDi;
        private float _phaNhay;

        void Awake()
        {
            var prefabNhanVat = Resources.Load<GameObject>("PTN/NhanVat/NhaNghienCuu");
            if (prefabNhanVat != null)
            {
                var moHinh = Instantiate(prefabNhanVat, transform, false);
                moHinh.name = "MoHinhMixamo";
                var animator = moHinh.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    Dau = animator.GetBoneTransform(HumanBodyBones.Head);
                }
                return;
            }

            DungMoHinhGiaoSu();
        }

        public void DungMoHinhGiaoSu()
        {
            // Vật liệu
            var vlDa = LabVatLieu.Dac(new Color(0.94f, 0.78f, 0.68f), 0.2f);           // Màu da
            var vlBlouse = LabVatLieu.Dac(new Color(0.97f, 0.97f, 0.99f), 0.35f);       // Áo blouse trắng
            var vlToc = LabVatLieu.Dac(new Color(0.72f, 0.72f, 0.75f), 0.1f);          // Tóc hoa râm
            var vlQuan = LabVatLieu.Dac(new Color(0.16f, 0.18f, 0.22f), 0.2f);          // Quần âu xám đậm
            var vlGiay = LabVatLieu.Dac(new Color(0.10f, 0.08f, 0.06f), 0.6f);          // Giày da nâu đen
            var vlKinh = LabVatLieu.ThuyTinh();                                        // Mặt kính
            var vlGong = LabVatLieu.KimLoai(new Color(0.15f, 0.15f, 0.18f));          // Gọng kính đen

            // 1. Gốc Thân & Áo Blouse
            var gThan = new GameObject("Than");
            gThan.transform.SetParent(transform, false);
            gThan.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            Than = gThan.transform;

            // Áo blouse dài qua gối (từ vai y=1.45 đến y=0.55m)
            var blouse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blouse.name = "AoBlouseTrang";
            blouse.transform.SetParent(Than, false);
            blouse.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            blouse.transform.localScale = new Vector3(0.48f, 0.45f, 0.32f);
            blouse.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(blouse.GetComponent<Collider>());

            // Cổ áo blouse & ve áo
            var coAo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coAo.name = "CoAo";
            coAo.transform.SetParent(Than, false);
            coAo.transform.localPosition = new Vector3(0f, 0.46f, 0.04f);
            coAo.transform.localScale = new Vector3(0.24f, 0.12f, 0.18f);
            coAo.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(coAo.GetComponent<Collider>());

            // Hai ve áo blouse chữ V
            var veTrai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            veTrai.name = "VeAoTrai";
            veTrai.transform.SetParent(Than, false);
            veTrai.transform.localPosition = new Vector3(-0.08f, 0.28f, 0.16f);
            veTrai.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            veTrai.transform.localScale = new Vector3(0.08f, 0.22f, 0.02f);
            veTrai.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(veTrai.GetComponent<Collider>());

            var vePhai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vePhai.name = "VeAoPhai";
            vePhai.transform.SetParent(Than, false);
            vePhai.transform.localPosition = new Vector3(0.08f, 0.28f, 0.16f);
            vePhai.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            vePhai.transform.localScale = new Vector3(0.08f, 0.22f, 0.02f);
            vePhai.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(vePhai.GetComponent<Collider>());

            // Hai túi áo blouse ngang hông
            var tuiTrai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tuiTrai.name = "TuiTrai";
            tuiTrai.transform.SetParent(Than, false);
            tuiTrai.transform.localPosition = new Vector3(-0.16f, -0.12f, 0.16f);
            tuiTrai.transform.localScale = new Vector3(0.12f, 0.12f, 0.02f);
            tuiTrai.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(tuiTrai.GetComponent<Collider>());

            var tuiPhai = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tuiPhai.name = "TuiPhai";
            tuiPhai.transform.SetParent(Than, false);
            tuiPhai.transform.localPosition = new Vector3(0.16f, -0.12f, 0.16f);
            tuiPhai.transform.localScale = new Vector3(0.12f, 0.12f, 0.02f);
            tuiPhai.GetComponent<Renderer>().material = vlBlouse;
            DestroyImmediate(tuiPhai.GetComponent<Collider>());

            // 2. Đầu, Tóc và Kính
            var gDau = new GameObject("Dau");
            gDau.transform.SetParent(Than, false);
            gDau.transform.localPosition = new Vector3(0f, 0.60f, 0f);
            Dau = gDau.transform;

            // Khuôn mặt
            var mat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mat.name = "KhuonMat";
            mat.transform.SetParent(Dau, false);
            mat.transform.localPosition = Vector3.zero;
            mat.transform.localScale = new Vector3(0.24f, 0.28f, 0.24f);
            mat.GetComponent<Renderer>().material = vlDa;
            DestroyImmediate(mat.GetComponent<Collider>());

            // Mái tóc hoa râm giáo sư
            var toc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            toc.name = "TocHoaRam";
            toc.transform.SetParent(Dau, false);
            toc.transform.localPosition = new Vector3(0f, 0.08f, -0.02f);
            toc.transform.localScale = new Vector3(0.26f, 0.18f, 0.26f);
            toc.GetComponent<Renderer>().material = vlToc;
            DestroyImmediate(toc.GetComponent<Collider>());

            // Kính lão / kính bảo hộ
            var gKinh = new GameObject("KinhBaoHo");
            gKinh.transform.SetParent(Dau, false);
            gKinh.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
            KinhBaoHo = gKinh;

            var matKinh1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matKinh1.transform.SetParent(gKinh.transform, false);
            matKinh1.transform.localPosition = new Vector3(-0.06f, 0f, 0f);
            matKinh1.transform.localScale = new Vector3(0.06f, 0.045f, 0.01f);
            matKinh1.GetComponent<Renderer>().material = vlKinh;
            DestroyImmediate(matKinh1.GetComponent<Collider>());

            var matKinh2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matKinh2.transform.SetParent(gKinh.transform, false);
            matKinh2.transform.localPosition = new Vector3(0.06f, 0f, 0f);
            matKinh2.transform.localScale = new Vector3(0.06f, 0.045f, 0.01f);
            matKinh2.GetComponent<Renderer>().material = vlKinh;
            DestroyImmediate(matKinh2.GetComponent<Collider>());

            var gongKinh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gongKinh.transform.SetParent(gKinh.transform, false);
            gongKinh.transform.localPosition = Vector3.zero;
            gongKinh.transform.localScale = new Vector3(0.18f, 0.01f, 0.01f);
            gongKinh.GetComponent<Renderer>().material = vlGong;
            DestroyImmediate(gongKinh.GetComponent<Collider>());

            // 3. Tay Trái & Tay Phải
            CanhTayTrai = TaoCanhTay("TayTrai", new Vector3(-0.30f, 0.40f, 0f), vlBlouse, vlDa, out var gtt);
            CanhTayPhai = TaoCanhTay("TayPhai", new Vector3(0.30f, 0.40f, 0f), vlBlouse, vlDa, out var gtp);
            GangTayTrai = gtt;
            GangTayPhai = gtp;

            // 4. Chân Trái & Chân Phải
            DuiTrai = TaoChan("ChanTrai", new Vector3(-0.13f, -0.40f, 0f), vlQuan, vlGiay);
            DuiPhai = TaoChan("ChanPhai", new Vector3(0.13f, -0.40f, 0f), vlQuan, vlGiay);

            // Gán layer 2 (Ignore Raycast) đệ quy cho toàn bộ nhân vật để camera không bao giờ bị va vào người
            GánLayerDeQuy(gameObject, 2);
        }

        private void GánLayerDeQuy(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform)
            {
                GánLayerDeQuy(c.gameObject, layer);
            }
        }

        private Transform TaoCanhTay(string ten, Vector3 vt, Material vlAo, Material vlTay, out GameObject gangTay)
        {
            var g = new GameObject(ten);
            g.transform.SetParent(Than, false);
            g.transform.localPosition = vt;

            var bapTay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bapTay.name = "BapTay";
            bapTay.transform.SetParent(g.transform, false);
            bapTay.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            bapTay.transform.localScale = new Vector3(0.12f, 0.24f, 0.12f);
            bapTay.GetComponent<Renderer>().material = vlAo;
            DestroyImmediate(bapTay.GetComponent<Collider>());

            var banTay = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            banTay.name = "BanTay";
            banTay.transform.SetParent(g.transform, false);
            banTay.transform.localPosition = new Vector3(0f, -0.50f, 0.02f);
            banTay.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
            banTay.GetComponent<Renderer>().material = vlTay;
            DestroyImmediate(banTay.GetComponent<Collider>());
            gangTay = banTay;

            return g.transform;
        }

        private Transform TaoChan(string ten, Vector3 vt, Material vlQuan, Material vlGiay)
        {
            var g = new GameObject(ten);
            g.transform.SetParent(Than, false);
            g.transform.localPosition = vt;

            var ongQuan = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ongQuan.name = "OngQuan";
            ongQuan.transform.SetParent(g.transform, false);
            ongQuan.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            ongQuan.transform.localScale = new Vector3(0.16f, 0.28f, 0.16f);
            ongQuan.GetComponent<Renderer>().material = vlQuan;
            DestroyImmediate(ongQuan.GetComponent<Collider>());

            var giay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            giay.name = "Giay";
            giay.transform.SetParent(g.transform, false);
            giay.transform.localPosition = new Vector3(0f, -0.53f, 0.04f);
            giay.transform.localScale = new Vector3(0.14f, 0.08f, 0.24f);
            giay.GetComponent<Renderer>().material = vlGiay;
            DestroyImmediate(giay.GetComponent<Collider>());

            return g.transform;
        }

        public void SetFirstPerson(bool fp)
        {
            if (Dau != null)
            {
                Dau.gameObject.SetActive(!fp);
                Dau.localScale = fp ? Vector3.zero : Vector3.one;
            }
        }

        public void ThucHienCuChi(SuKienLab sk)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                string clip = BangTrangThaiNhanVat.AnhXa(sk);
                anim.CrossFade(clip, 0.2f);
            }
        }

        public void SetHienThi(bool hien)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = hien;
            }
        }

        public void DeoDoBaoHo()
        {
            DaDeoBaoHo = true;
            if (GangTayTrai != null) GangTayTrai.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.2f, 0.5f, 0.85f), 0.5f);
            if (GangTayPhai != null) GangTayPhai.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.2f, 0.5f, 0.85f), 0.5f);
            Debug.Log("[DungNhanVat] Đã trang bị kính bảo hộ & găng tay cao su y tế!");
        }

        public void CapNhatHoatHinh(float tocDo, bool trenMatDat, float dt)
        {
            if (Than == null || DuiTrai == null || DuiPhai == null || CanhTayTrai == null || CanhTayPhai == null) return;

            if (!trenMatDat)
            {
                // Tư thế nhảy
                _phaNhay = Mathf.Lerp(_phaNhay, 1f, dt * 10f);
                DuiTrai.localRotation = Quaternion.Euler(25f * _phaNhay, 0f, 0f);
                DuiPhai.localRotation = Quaternion.Euler(15f * _phaNhay, 0f, 0f);
                CanhTayTrai.localRotation = Quaternion.Euler(-35f * _phaNhay, 0f, -15f);
                CanhTayPhai.localRotation = Quaternion.Euler(-35f * _phaNhay, 0f, 15f);
                return;
            }

            _phaNhay = Mathf.Lerp(_phaNhay, 0f, dt * 12f);

            if (tocDo > 0.05f)
            {
                // Bước đi / chạy
                float tanSo = tocDo > 3.5f ? 14f : 8f;
                _phaDi += dt * tanSo;

                float bienDoChan = tocDo > 3.5f ? 45f : 30f;
                float bienDoTay = tocDo > 3.5f ? 40f : 25f;

                float gocChan = Mathf.Sin(_phaDi) * bienDoChan;
                float gocTay = -Mathf.Sin(_phaDi) * bienDoTay;

                DuiTrai.localRotation = Quaternion.Euler(gocChan, 0f, 0f);
                DuiPhai.localRotation = Quaternion.Euler(-gocChan, 0f, 0f);

                CanhTayTrai.localRotation = Quaternion.Euler(gocTay, 0f, 0f);
                CanhTayPhai.localRotation = Quaternion.Euler(-gocTay, 0f, 0f);

                // Độ nhấp nhô nhẹ của thân
                float nhapNho = Mathf.Abs(Mathf.Sin(_phaDi * 2f)) * 0.035f;
                Than.localPosition = new Vector3(0f, 0.95f + nhapNho, 0f);
            }
            else
            {
                // Đứng yên thở nhẹ
                _phaDi += dt * 2.2f;
                float tho = Mathf.Sin(_phaDi) * 0.008f;
                Than.localPosition = new Vector3(0f, 0.95f + tho, 0f);

                DuiTrai.localRotation = Quaternion.Lerp(DuiTrai.localRotation, Quaternion.identity, dt * 8f);
                DuiPhai.localRotation = Quaternion.Lerp(DuiPhai.localRotation, Quaternion.identity, dt * 8f);
                CanhTayTrai.localRotation = Quaternion.Lerp(CanhTayTrai.localRotation, Quaternion.identity, dt * 8f);
                CanhTayPhai.localRotation = Quaternion.Lerp(CanhTayPhai.localRotation, Quaternion.identity, dt * 8f);
            }
        }
    }
}
