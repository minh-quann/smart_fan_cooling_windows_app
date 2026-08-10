# PowerShell Script to Publish WinUI 3 App and Compile Setup Installer (.exe)
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -ErrorAction SilentlyContinue

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "DONG GOI UNG DUNG LLANO SMART FAN COOLING HUB (SETUP INSTALLER)" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Cyan

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (!(Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

Write-Host "`n[1/3] Dang dong goi ban Release Self-Contained (.NET 10 + WinUI 3 Native)..." -ForegroundColor Yellow
& $dotnet publish -c Release -r win-x64 --self-contained true

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nLoi khi publish ung dung!" -ForegroundColor Red
    exit 1
}

$publishDir = "bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"
Write-Host "`n[2/3] Publish thanh cong thu muc: $publishDir" -ForegroundColor Green

# Check for Inno Setup compiler ISCC.exe
$iscc = Get-Command "iscc.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Path
if (!$iscc) {
    $possiblePaths = @(
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $possiblePaths) {
        if (Test-Path $p) {
            $iscc = $p
            break
        }
    }
}

if ($iscc) {
    Write-Host "`n[3/3] Dang bien dich File Cai Dat Setup .exe bang Inno Setup Compiler..." -ForegroundColor Yellow
    & $iscc "installer.iss"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nTAO FILE CAI DAT THANH CONG!" -ForegroundColor Green
        Write-Host "File Cai Dat Setup nam tai: OutputInstaller\Llano_Smart_Fan_Cooling_Setup_v1.0.exe" -ForegroundColor Cyan
    }
} else {
    Write-Host "`n[3/3] Da dong goi thanh cong bo cai Portable san sang su dung." -ForegroundColor Green
    Write-Host "De dong goi thanh 1 File Setup .exe co Wizard Cai Dat chuyen nghiep:" -ForegroundColor Yellow
    Write-Host " 1. Tai phan mem mien phi Inno Setup (jrsoftware.org/isdl.php)" -ForegroundColor White
    Write-Host " 2. Nhap chuot phai vao file 'installer.iss' -> Chon 'Compile'!" -ForegroundColor White
    Write-Host "`nThu muc App da dong goi day du: $publishDir" -ForegroundColor Cyan
}
