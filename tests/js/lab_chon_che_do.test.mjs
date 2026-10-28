import { test } from "node:test";
import assert from "node:assert/strict";
import chonModule from "../../frontend/hoa-hoc/phong-thi-nghiem/chon-che-do-lab.js";

const { chonCheDo } = chonModule;

test("chonCheDo: Không phải app -> chọn nhẹ", () => {
  const kq = chonCheDo({ laApp: false });
  assert.equal(kq.cheDo, "nhe");
});

test("chonCheDo: App nhưng build.coSan = false -> chọn nhẹ", () => {
  const kq = chonCheDo({
    laApp: true,
    build: { coSan: false },
    webgl2: true
  });
  assert.equal(kq.cheDo, "nhe");
});

test("chonCheDo: Không có WebGL2 -> chọn nhẹ", () => {
  const kq = chonCheDo({
    laApp: true,
    build: { coSan: true },
    webgl2: false
  });
  assert.equal(kq.cheDo, "nhe");
});

test("chonCheDo: app luôn dùng Unity dù GPU phần mềm / máy yếu / ưu tiên cũ là 'nhe'", () => {
  const co = { laApp: true, build: { coSan: true }, webgl2: true };
  assert.equal(chonCheDo({ ...co, rendererGpu: "Google SwiftShader (Subzero) Renderer" }).cheDo, "unity");
  assert.equal(chonCheDo({ ...co, rendererGpu: "llvmpipe (LLVM 12.0.0, 256 bits)" }).cheDo, "unity");
  assert.equal(chonCheDo({ ...co, soNhan: 2, ramGB: 4 }).cheDo, "unity");
  assert.equal(chonCheDo({ ...co, uuTien: "nhe" }).cheDo, "unity");
});

test("chonCheDo: Máy mạnh + build đủ -> chọn unity", () => {
  const kq = chonCheDo({
    laApp: true,
    build: { coSan: true },
    webgl2: true,
    rendererGpu: "Apple M1",
    soNhan: 8,
    ramGB: 8
  });
  assert.equal(kq.cheDo, "unity");
});
