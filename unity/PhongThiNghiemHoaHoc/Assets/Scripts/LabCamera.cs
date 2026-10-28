// Camera quỹ đạo có giới hạn góc và khoảng cách; kéo chuột xoay, cuộn phóng to; nhận lệnh nút bấm từ trang web
// (trai / phai / len / xuong / gan / xa / dat_lai) – nút bấm thay thế cho kéo / xoay.
using UnityEngine;

namespace PhongThiNghiem
{
    public class LabCamera : MonoBehaviour
    {
        public Vector3 dich = new Vector3(0f, 0.2f, 0f);
        public float khoangCach = 1.4f, yaw = 0f, pitch = 25f, minD = 0.25f, maxD = 4f;

        Vector3 _dichMuon;
        float _kcMuon, _yawMuon, _pitchMuon;
        Vector3 _gocViTri, _gocDich;
        Vector3 _chuot;
        bool _keo;

        public bool VuaKeo => _keo;

        void Awake()
        {
            _dichMuon = dich; _kcMuon = khoangCach; _yawMuon = yaw; _pitchMuon = pitch;
        }

        public void DatGoc(Vector3 viTri, Vector3 d, float gan, float xa)
        {
            _gocViTri = viTri; _gocDich = d;
            minD = gan; maxD = Mathf.Max(xa, (viTri - d).magnitude * 1.4f);
            Vector3 o = viTri - d;
            float kc = o.magnitude;
            _dichMuon = d; _kcMuon = kc;
            _pitchMuon = Mathf.Asin(Mathf.Clamp(o.y / kc, -1f, 1f)) * Mathf.Rad2Deg;
            _yawMuon = Mathf.Atan2(-o.x, -o.z) * Mathf.Rad2Deg;
            dich = _dichMuon; khoangCach = _kcMuon; pitch = _pitchMuon; yaw = _yawMuon;   // đổi thí nghiệm: đặt ngay
            DatViTri();
        }

        public void Lenh(string l)
        {
            switch (l)
            {
                case "trai": _yawMuon -= 17f; break;
                case "phai": _yawMuon += 17f; break;
                case "len": _pitchMuon = Mathf.Clamp(_pitchMuon + 8f, 5f, 80f); break;
                case "xuong": _pitchMuon = Mathf.Clamp(_pitchMuon - 8f, 5f, 80f); break;
                case "gan": _kcMuon = Mathf.Clamp(_kcMuon * 0.82f, minD, maxD); break;
                case "xa": _kcMuon = Mathf.Clamp(_kcMuon * 1.2f, minD, maxD); break;
                case "dat_lai":
                    Vector3 o = _gocViTri - _gocDich;
                    if (o.sqrMagnitude < 1e-6f) break;
                    _dichMuon = _gocDich; _kcMuon = o.magnitude;
                    _pitchMuon = Mathf.Asin(Mathf.Clamp(o.y / _kcMuon, -1f, 1f)) * Mathf.Rad2Deg;
                    _yawMuon = Mathf.Atan2(-o.x, -o.z) * Mathf.Rad2Deg;
                    break;
            }
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0)) { _chuot = Input.mousePosition; _keo = false; }
            if (Input.GetMouseButton(0))
            {
                Vector3 d = Input.mousePosition - _chuot;
                if (d.magnitude > 3f) _keo = true;
                if (_keo)
                {
                    _yawMuon -= d.x * 0.25f;
                    _pitchMuon = Mathf.Clamp(_pitchMuon - d.y * 0.2f, 5f, 80f);
                }
                _chuot = Input.mousePosition;
            }
            float cuon = Input.mouseScrollDelta.y;
            if (Mathf.Abs(cuon) > 0.01f) _kcMuon = Mathf.Clamp(_kcMuon * (1f - cuon * 0.1f), minD, maxD);

            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, _yawMuon, k);
            pitch = Mathf.Lerp(pitch, _pitchMuon, k);
            khoangCach = Mathf.Lerp(khoangCach, _kcMuon, k);
            dich = Vector3.Lerp(dich, _dichMuon, k);
            DatViTri();
        }

        void LateUpdate()
        {
            if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) _keo = false;
        }

        void DatViTri()
        {
            Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = dich + q * new Vector3(0f, 0f, -khoangCach);
            transform.LookAt(dich);
        }
    }
}
