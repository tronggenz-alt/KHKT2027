// Danh mục tĩnh đọc thẳng từ data/ của dự án (đóng gói vào Function lúc dựng), như bản FastAPI đọc JSON lúc khởi động.

import hoaChat from "../../data/chem/hoa_chat.json";
import thietBi from "../../data/chem/thiet_bi.json";
import phanUng from "../../data/reactions/phan_ung.json";
import dungCuSinh from "../../data/sinh/dung_cu.json";
import baiSinh from "../../data/sinh/bai_thi_nghiem.json";
import { MAY } from "./may_du_lieu";

export const CHEM: any = hoaChat;
export const EQUIP: any = thietBi;
export const CHEM_BY_STT = new Map<number, any>(CHEM.hoa_chat.map((c: any) => [c.stt, c]));
export const EQUIP_BY_STT = new Map<number, any>(EQUIP.thiet_bi.map((e: any) => [e.stt, e]));
export const REACTIONS = new Map<string, any>((phanUng as any).phan_ung.map((r: any) => [r.id, r]));

export const DUNG_CU_SINH: any[] = (dungCuSinh as any).dung_cu || [];
export const DUNG_CU_SINH_THEO_MA = new Map<string, any>(DUNG_CU_SINH.map((d) => [d.ma, d]));
export const BAI_SINH: any[] = (baiSinh as any).bai_thi_nghiem || [];

// Hồ sơ vận hành các máy (data/may/*.json, chỉ những tệp có quy_trinh)
export const MACHINES = new Map<string, any>(MAY.filter((d: any) => d && d.quy_trinh).map((d: any) => [d.ma, d]));
