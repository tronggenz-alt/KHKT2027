mergeInto(LibraryManager.library, {
    PTN_Tao: function (idPtr, cheDoPtr) {
        var id = UTF8ToString(idPtr);
        var cheDo = UTF8ToString(cheDoPtr);
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.tao === 'function') {
            window.PTNLoiTrang.tao(id, cheDo);
        }
    },

    PTN_ThaoTac: function (idPtr, lenhPtr, jsonPtr) {
        var id = UTF8ToString(idPtr);
        var lenh = UTF8ToString(lenhPtr);
        var json = UTF8ToString(jsonPtr);
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.thaoTac === 'function') {
            window.PTNLoiTrang.thaoTac(id, lenh, json);
        }
    },

    PTN_TrangThai: function (idPtr) {
        var id = UTF8ToString(idPtr);
        var res = "{}";
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.trangThai === 'function') {
            res = window.PTNLoiTrang.trangThai(id) || "{}";
        }
        var lengthBytes = lengthBytesUTF8(res) + 1;
        var buffer = _malloc(lengthBytes);
        stringToUTF8(res, buffer, lengthBytes);
        return buffer;
    },

    PTN_SoTay: function (idPtr) {
        var id = UTF8ToString(idPtr);
        var res = "[]";
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.soTay === 'function') {
            res = window.PTNLoiTrang.soTay(id) || "[]";
        }
        var lengthBytes = lengthBytesUTF8(res) + 1;
        var buffer = _malloc(lengthBytes);
        stringToUTF8(res, buffer, lengthBytes);
        return buffer;
    },

    PTN_ChoChay: function (choChay) {
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.choChay === 'function') {
            window.PTNLoiTrang.choChay(!!choChay);
        }
    },

    PTN_BaoSuKien: function (tenPtr, jsonPtr) {
        var ten = UTF8ToString(tenPtr);
        var json = UTF8ToString(jsonPtr);
        if (window.PTNLoiTrang && typeof window.PTNLoiTrang.nhanSuKien === 'function') {
            window.PTNLoiTrang.nhanSuKien(ten, json);
        }
    }
});
