using UnityEngine;

namespace PhongThiNghiem
{
    public interface ICauNoiLoi
    {
        bool Toi { get; }
        Color MauNhan { get; }
        void GuiThaoTac(string lenh, string thamSoJson = null);
        void GuiChon(string loai, int so);
    }
}
