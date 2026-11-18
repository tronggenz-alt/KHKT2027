using System;
using System.Reflection;
using UnityEngine;

namespace Khkt.Bio.ThucVat
{
    /// <summary>
    /// Cầu nối hợp đồng đúng tên Khkt.Bio.ThucVat.ThucVatMoDun nằm trong assembly riêng (autoReferenced = false)
    /// để bảng điều khiển C4 tìm được qua AppDomain.CurrentDomain.GetAssemblies() mà không gây xung đột với MenuKit.Bio trong MainMenu.cs.
    /// </summary>
    public static class ThucVatMoDun
    {
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
                var t = assemblies[i].GetType("Khkt.Demo.Bio.ThucVat.ThucVatMoDunImpl", throwOnError: false);
                if (t == null) continue;
                implType = t;
                pDangMo = t.GetProperty("DangMo", BindingFlags.Public | BindingFlags.Static);
                mMo = t.GetMethod("Mo", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
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

        public static void Mo()
        {
            EnsureBound();
            mMo?.Invoke(null, null);
        }

        public static void Dong()
        {
            EnsureBound();
            mDong?.Invoke(null, null);
        }
    }
}
