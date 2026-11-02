// Tách từ SlidePrepTools.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using UnityEngine;
using Khkt.Interaction;
using Khkt.Interaction.Bio;
using Khkt.Demo.Instruments;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// 5.1.6. Giấy lau kính chuyên dụng — mã: giay_lau_kinh
    /// </summary>
    [DisallowMultipleComponent]
    public class LensTissue : MonoBehaviour, IInteractable
    {
        public BioTool bioTool;
        public bool isUsed = false;

        void Awake()
        {
            bioTool = GetComponent<BioTool>();
            if (bioTool == null) bioTool = gameObject.AddComponent<BioTool>();
            bioTool.toolId = "giay_lau_kinh";
            bioTool.toolDisplayName = "Giấy lau kính chuyên dụng";
            bioTool.kind = BioToolKind.LensPaper;
        }

        public string UseHint => "Giấy lau kính chuyên dụng · Dùng lau sạch dầu soi trên vật kính 100× và 40×";

        public void Use(GameObject user)
        {
            isUsed = true;
            var scopes = FindObjectsByType<MicroscopeController>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (Vector3.Distance(transform.position, scopes[i].transform.position) < 1.2f)
                {
                    scopes[i].CleanObjectiveWithLensTissue();
                    return;
                }
            }
            BioFeedback.PlayClick();
            BioFeedback.LogAction("giay_lau_kinh", "lau_vat_kinh", "Đã dùng giấy lau kính chuyên dụng lau sạch vật kính hiển vi.");
        }
    }
}
