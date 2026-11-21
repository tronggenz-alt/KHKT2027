# Nhập khóa API AI cho bài chụp ảnh trò chuyện với trợ lý (chay.ps1 -Mode anh_chat).
# Khóa gõ ẩn, được mã hóa DPAPI (chỉ tài khoản Windows này giải được) vào tệp tạm ngoài kho mã;
# bài thử đọc xong là xóa tệp ngay. Không in khóa ra màn hình.
Add-Type -AssemblyName System.Security
$tep = "D:\KHKT_kiem_thu\khoa_ai_tam.bin"
Write-Host "Chọn nhà cung cấp: 1 = Google Gemini, 2 = Groq, 3 = Anthropic Claude, 4 = OpenAI"
$chon = Read-Host "Số"
$ncc = switch ($chon) { "1" { "gemini" } "2" { "groq" } "3" { "claude" } "4" { "openai" } default { "" } }
if (-not $ncc) { Write-Host "Chưa chọn đúng nhà cung cấp."; exit 1 }
$moHinh = Read-Host "Mô hình (Enter để dùng mặc định)"
$an = Read-Host "Dán khóa API (không hiện ký tự)" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($an)
try { $khoa = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr).Trim() }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
if ($khoa.Length -lt 10) { Write-Host "Khóa quá ngắn, chưa lưu."; exit 1 }
$json = @{ nha_cung_cap = $ncc; mo_hinh = $moHinh.Trim(); khoa = $khoa } | ConvertTo-Json -Compress
$khoa = $null
$byte = [Text.Encoding]::UTF8.GetBytes($json)
$json = $null
$maHoa = [Security.Cryptography.ProtectedData]::Protect($byte, [Text.Encoding]::UTF8.GetBytes("vera-anh-chat"), [Security.Cryptography.DataProtectionScope]::CurrentUser)
[Array]::Clear($byte, 0, $byte.Length)
[IO.File]::WriteAllBytes($tep, $maHoa)
Write-Host "Đã lưu khóa $ncc (đã mã hóa) vào tệp tạm. Bài thử đọc xong sẽ xóa tệp. Bạn có thể đóng tab này."
