"""Dựng poster: tam_*.src.html -> PDF từng tấm (Chrome headless) -> gộp Poster_VERA_Lab.pdf + ảnh xem trước."""
import pathlib, subprocess, sys

D = pathlib.Path(__file__).resolve().parent
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
ICONS = (D / "icons.svg").read_text(encoding="utf8")
TAMS = [("tam_1_trai", "500mm 1500mm"), ("tam_2_giua", "1000mm 1500mm"), ("tam_3_phai", "500mm 1500mm")]
out = D / "out"; out.mkdir(exist_ok=True)

pdfs = []
for name, size in TAMS:
    w, h = size.split()
    src = (D / f"{name}.src.html").read_text(encoding="utf8")
    head = (f'<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>VERA Lab – {name}</title>'
            f'<link rel="stylesheet" href="poster.css"><style>@page{{size:{size};margin:0}}'
            f'.panel{{width:{w};height:{h}}}</style></head><body>{ICONS}')
    html = D / f"{name}.html"
    html.write_text(head + src + "</body></html>", encoding="utf8")
    pdf = out / f"{name}.pdf"
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
                    "--allow-file-access-from-files", "--virtual-time-budget=8000",
                    f"--print-to-pdf={pdf}", html.as_uri()], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    n = subprocess.run(["pdfinfo", str(pdf)], capture_output=True, text=True).stdout
    pages = [l for l in n.splitlines() if l.startswith("Pages")][0]
    print(name, pages.split()[-1], "trang")
    pdfs.append(str(pdf))

final = out / "Poster_VERA_Lab.pdf"
subprocess.run(["pdfunite", *pdfs, str(final)], check=True)
dpi = sys.argv[1] if len(sys.argv) > 1 else "24"
for i, (name, _) in enumerate(TAMS):
    subprocess.run(["pdftoppm", "-r", dpi, "-f", "1", "-l", "1", "-png", "-singlefile", pdfs[i], str(out / f"xem_{name}")], check=True)
print("xong:", final)
