using System;
using UnityEngine;

namespace PhongThiNghiem.ThiNghiem
{
    /// <summary>
    /// Điểm nóng tương tác (Hotspots) trên dụng cụ thí nghiệm:
    /// - Icon tròn nổi 3D có hiệu ứng nhịp thở (pulse animation)
    /// - Hiển thị tooltip chức năng khi di chuột hoặc lại gần
    /// - Kích hoạt sự kiện tương tác khi click
    /// </summary>
    public class DiemNongTuongTac : MonoBehaviour
    {
        public string MoTaTooltip = "Thao tác dụng cụ";
        public float TocDoNhipTho = 2.5f;
        public float BienDoNhipTho = 0.18f;
        public Action OnClickAction;

        private Vector3 _kichThuocGoc;
        private bool _dangHover = false;
        private Renderer _renderer;
        private Color _mauGoc;

        void Start()
        {
            _kichThuocGoc = transform.localScale;
            _renderer = GetComponent<Renderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _mauGoc = _renderer.material.color;
            }
        }

        void Update()
        {
            // Hiệu ứng nhịp thở nhẹ nhàng (Pulse animation)
            float t = Time.time * TocDoNhipTho;
            float scale = 1.0f + Mathf.Sin(t) * BienDoNhipTho;
            if (_dangHover) scale *= 1.25f;

            transform.localScale = _kichThuocGoc * scale;

            // Luôn xoay mặt về camera chính
            var cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }

        void OnMouseEnter()
        {
            _dangHover = true;
            if (_renderer != null)
            {
                _renderer.material = LabVatLieu.PhatSang(new Color(0.22f, 0.74f, 0.97f)); // Sky Blue sáng
            }
        }

        void OnMouseExit()
        {
            _dangHover = false;
            if (_renderer != null)
            {
                _renderer.material = LabVatLieu.PhatSang(_mauGoc);
            }
        }

        void OnMouseDown()
        {
            OnClickAction?.Invoke();
            Debug.Log($"[DiemNongTuongTac] Kích hoạt điểm nóng: {MoTaTooltip}");
        }

        void OnGUI()
        {
            if (!_dangHover) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            if (screenPos.z < 0) return;

            float x = screenPos.x;
            float y = Screen.height - screenPos.y - 30f;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.backgroundColor = new Color(0.05f, 0.15f, 0.28f, 0.92f);
            GUI.Box(new Rect(x - 110, y, 220, 26), MoTaTooltip, style);
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Tạo điểm nóng tương tác gắn vào vị trí chỉ định.
        /// </summary>
        public static GameObject TaoDiemNong(Transform parent, string moTa, Vector3 viTri)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DiemNong_" + moTa;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = viTri;
            go.transform.localScale = new Vector3(0.024f, 0.024f, 0.024f);

            var dn = go.AddComponent<DiemNongTuongTac>();
            dn.MoTaTooltip = moTa;

            var mat = LabVatLieu.PhatSang(new Color(1.0f, 0.85f, 0.2f, 0.9f)); // Vàng cam
            go.GetComponent<Renderer>().material = mat;

            return go;
        }
    }
}
