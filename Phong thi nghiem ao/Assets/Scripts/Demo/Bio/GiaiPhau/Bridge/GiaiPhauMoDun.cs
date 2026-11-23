using System;
using System.Reflection;
using UnityEngine;

namespace Khkt.Bio.GiaiPhau
{
    /// <summary>
    /// Cầu nối hợp đồng đúng tên Khkt.Bio.GiaiPhau.GiaiPhauMoDun đặt trong assembly riêng (autoReferenced = false)
    /// để không xung đột với MenuKit.Bio trong MainMenu.cs nhưng vẫn truy cập được qua reflection ở mọi nơi.
    /// 19/11: mở bản web BIO-LAB 3D cũ của CHEMISTRY (Khkt.Demo.Bio.Portal.BioWebViewBridge) thay cho mô-đun G4
    /// GiaiPhauMoDunImpl (mã G4 vẫn giữ trong kho nhưng không dùng nữa).
    /// </summary>
    public static class GiaiPhauMoDun
    {
        const string LopCauNoi = "Khkt.Demo.Bio.Portal.BioWebViewBridge";
        static Type implType;
        static PropertyInfo pDangMo;
        static MethodInfo mMo, mDong;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void RegisterAssembly() => EnsureBound();

        static void EnsureBound()
        {
            if (implType != null) return;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var t = assemblies[i].GetType(LopCauNoi, throwOnError: false);
                if (t == null) continue;
                implType = t;
                pDangMo = t.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Static);
                mMo = t.GetMethod("Open", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Action) }, null);
                mDong = t.GetMethod("Close", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                break;
            }
        }

        public static bool DangMo
        {
            get
            {
                EnsureBound();
                return pDangMo != null && (bool)pDangMo.GetValue(null);
            }
        }

        public static void Mo()
        {
            EnsureBound();
            mMo?.Invoke(null, new object[] { null });
        }

        public static void Dong()
        {
            EnsureBound();
            mDong?.Invoke(null, null);
        }
    }
}
