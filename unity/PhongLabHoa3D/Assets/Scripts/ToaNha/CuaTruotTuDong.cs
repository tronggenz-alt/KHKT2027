using UnityEngine;
using PhongThiNghiem.NhanVat;

namespace PhongThiNghiem.ToaNha
{
    /// <summary>
    /// Điều khiển cửa trượt kính tự động mở khi nhân vật lại gần (< 2.2m)
    /// và đóng lại khi nhân vật đi xa (> 2.8m).
    /// </summary>
    public class CuaTruotTuDong : MonoBehaviour
    {
        public Transform CanhTrai;
        public Transform CanhPhai;
        public float DoMoRong = 0.85f;
        public float TocDoTruot = 3.5f;
        public float KhoangCachKichHoat = 2.4f;

        private Vector3 _viTriGocTrai;
        private Vector3 _viTriGocPhai;
        private Vector3 _viTriMoTrai;
        private Vector3 _viTriMoPhai;

        public bool DangMo { get; private set; }

        void Start()
        {
            if (CanhTrai != null)
            {
                _viTriGocTrai = CanhTrai.localPosition;
                _viTriMoTrai = _viTriGocTrai - new Vector3(DoMoRong, 0f, 0f);
            }

            if (CanhPhai != null)
            {
                _viTriGocPhai = CanhPhai.localPosition;
                _viTriMoPhai = _viTriGocPhai + new Vector3(DoMoRong, 0f, 0f);
            }
        }

        void Update()
        {
            Vector3 viTriNguoi = Vector3.zero;
            if (DieuKhienNhanVat.Instance != null)
            {
                viTriNguoi = DieuKhienNhanVat.Instance.transform.position;
            }
            else
            {
                var nv = GameObject.Find("NhanVatGiaoSu");
                if (nv != null) viTriNguoi = nv.transform.position;
            }

            float khoangCach = Vector3.Distance(transform.position, viTriNguoi);
            DangMo = (khoangCach <= KhoangCachKichHoat);

            float dt = Time.deltaTime * TocDoTruot;

            if (CanhTrai != null)
            {
                Vector3 mucTieu = DangMo ? _viTriMoTrai : _viTriGocTrai;
                CanhTrai.localPosition = Vector3.MoveTowards(CanhTrai.localPosition, mucTieu, dt);
            }

            if (CanhPhai != null)
            {
                Vector3 mucTieu = DangMo ? _viTriMoPhai : _viTriGocPhai;
                CanhPhai.localPosition = Vector3.MoveTowards(CanhPhai.localPosition, mucTieu, dt);
            }
        }
    }
}
