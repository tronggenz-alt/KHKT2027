// data/index.js – danh mục các quy trình có sẵn (thêm quy trình mới: tạo file dữ liệu cùng mẫu rồi khai báo ở đây).
import TUAN_HOAN_NHO from "./vong-tuan-hoan-nho.js";
import TUAN_HOAN_LON from "./vong-tuan-hoan-lon.js";

export const QUY_TRINH = Object.freeze({
  [TUAN_HOAN_NHO.id]: TUAN_HOAN_NHO,
  [TUAN_HOAN_LON.id]: TUAN_HOAN_LON,
  pulmonary_circulation: TUAN_HOAN_NHO,
  systemic_circulation: TUAN_HOAN_LON,
  vong_tuan_hoan_nho: TUAN_HOAN_NHO,
  vong_tuan_hoan_lon: TUAN_HOAN_LON,
});
export const QUY_TRINH_MAC_DINH = TUAN_HOAN_NHO.id;
