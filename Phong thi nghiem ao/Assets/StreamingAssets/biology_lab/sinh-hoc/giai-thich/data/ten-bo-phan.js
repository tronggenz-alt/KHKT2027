// data/ten-bo-phan.js – đổi tên bộ phận tiếng Anh / tên gọi khác (AI hay dùng) sang id bộ phận của mô hình 3D.
export const TEN_KHAC = Object.freeze({
  right_ventricle: "tam_that_phai", left_ventricle: "tam_that_trai",
  right_atrium: "tam_nhi_phai", left_atrium: "tam_nhi_trai",
  pulmonary_artery: "dong_mach_phoi", pulmonary_arteries: "dong_mach_phoi", pulmonary_trunk: "dong_mach_phoi",
  pulmonary_vein: "tinh_mach_phoi", pulmonary_veins: "tinh_mach_phoi",
  aorta: "dong_mach_chu", aortic_arch: "dong_mach_chu",
  vena_cava: "tinh_mach_chu", inferior_vena_cava: "tinh_mach_chu", superior_vena_cava: "tinh_mach_chu",
  right_lung: "phoi_phai", left_lung: "phoi_trai",
  brain: "dai_nao", cerebrum: "dai_nao", cerebellum: "tieu_nao", brainstem: "than_nao", spinal_cord: "tuy_song",
  stomach: "da_day", liver: "gan", gallbladder: "tui_mat", pancreas: "tuy", spleen: "lach",
  esophagus: "thuc_quan", oesophagus: "thuc_quan", duodenum: "ta_trang", small_intestine: "ruot_non",
  large_intestine: "dai_trang", colon: "dai_trang", rectum: "truc_trang", appendix: "ruot_thua",
  left_kidney: "than_trai", right_kidney: "than_phai", kidney: "than_trai",
  ureter: "nieu_quan", ureters: "nieu_quan", bladder: "bang_quang", urinary_bladder: "bang_quang", urethra: "nieu_dao",
  adrenal: "tuyen_thuong_than", adrenal_gland: "tuyen_thuong_than",
  trachea: "khi_quan", bronchus: "phe_quan", bronchi: "phe_quan", larynx: "thanh_quan", diaphragm: "co_hoanh",
  pelvic_vessels: "mach_chau", iliac_vessels: "mach_chau", renal_vessels: "mach_than",
  quadriceps: "co_tu_dau_dui", thigh_muscle: "co_tu_dau_dui",
});
// Tên nhóm (một tên → nhiều bộ phận)
export const NHOM_TEN = Object.freeze({
  lungs: ["phoi_phai", "phoi_trai"], phoi: ["phoi_phai", "phoi_trai"],
  kidneys: ["than_trai", "than_phai"], than: ["than_trai", "than_phai"],
});
