using System;
using System.Reflection;
using UnityEngine;

namespace Khkt.Bio.GiaiPhau
{
    /// <summary>
    /// Cầu nối hợp đồng đúng tên Khkt.Bio.GiaiPhau.GiaiPhauMoDun đặt trong assembly riêng (autoReferenced = false)
    /// để không xung đột với MenuKit.Bio trong MainMenu.cs nhưng vẫn truy cập được qua reflection ở mọi nơi.
    /// 24/11: mở khung xem giải phẫu 3D ngay trong app (Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl: BodyParts3D, bóc lớp,
    /// mặt cắt, tim đập, phổi thở, nhu động, câu hỏi chỉ cơ quan; từ đó sang được bàn mổ tự do). Không mở trang web nữa.
    /// </summary>
    public static class GiaiPhauMoDun
    {
        const string LopThucThi = "Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl";
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
                var t = assemblies[i].GetType(LopThucThi, throwOnError: false);
                if (t == null) continue;
                implType = t;
                pDangMo = t.GetProperty("DangMo", BindingFlags.Public | BindingFlags.Static);
                mMo = t.GetMethod("Mo", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Action) }, null);
                mDong = t.GetMethod("Dong", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
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

        public static void Mo() => Mo(null);

        public static void Mo(Action onClosed)
        {
            EnsureBound();
            mMo?.Invoke(null, new object[] { onClosed });
        }

        public static void Dong()
        {
            EnsureBound();
            mDong?.Invoke(null, null);
        }
    }
}
