// Cầu nối Unity → trang web: C# gọi PTN_GuiSuKien(json) → window.PhongLabUnity.nhan(json) (cau-unity.js).
mergeInto(LibraryManager.library, {
  PTN_GuiSuKien: function (ptr) {
    var json = UTF8ToString(ptr);
    try {
      if (typeof window !== "undefined" && window.PhongLabUnity && typeof window.PhongLabUnity.nhan === "function") {
        window.PhongLabUnity.nhan(json);
      }
    } catch (e) {
      console.error("[LabBridge.jslib]", e);
    }
  }
});
