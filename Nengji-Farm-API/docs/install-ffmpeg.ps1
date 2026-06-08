# 一键安装 ffmpeg 并添加到系统 PATH
# 以管理员身份运行此脚本

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host ">>> $msg" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  ! $msg" -ForegroundColor Yellow }

# 1. 检查是否已有 ffmpeg
Write-Step "检查 ffmpeg..."
$existing = Get-Command ffmpeg -ErrorAction SilentlyContinue
if ($existing) {
    $ver = & ffmpeg -version 2>&1 | Select-Object -First 1
    Write-OK "ffmpeg 已安装: $ver"
    exit 0
}

# 2. 下载 ffmpeg
$installDir = "C:\ffmpeg"
$zipPath    = "$env:TEMP\ffmpeg-release.zip"
$downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"

Write-Step "下载 ffmpeg (约 50MB)..."
try {
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($downloadUrl, $zipPath)
    Write-OK "下载完成: $zipPath"
} catch {
    Write-Warn "官方源下载失败，尝试 GitHub 镜像..."
    $downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
    $zipPath = "$env:TEMP\ffmpeg-github.zip"
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($downloadUrl, $zipPath)
    Write-OK "下载完成: $zipPath"
}

# 3. 解压
Write-Step "解压到 $installDir ..."
if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $installDir)
Remove-Item $zipPath -Force

# 4. 找到 ffmpeg.exe 所在目录
$binDir = Get-ChildItem $installDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1 -ExpandProperty DirectoryName
if (-not $binDir) { throw "未找到 ffmpeg.exe" }
Write-OK "ffmpeg.exe 位于: $binDir"

# 5. 添加到系统 PATH
Write-Step "添加到系统 PATH..."
$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($machinePath -notlike "*$binDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$machinePath;$binDir", "Machine")
    Write-OK "已添加到系统 PATH"
} else {
    Write-OK "已在 PATH 中"
}

# 6. 刷新当前会话 PATH
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine")

# 7. 验证
Write-Step "验证安装..."
$ver = & ffmpeg -version 2>&1 | Select-Object -First 1
if ($LASTEXITCODE -eq 0) {
    Write-OK "ffmpeg 安装成功!"
    Write-Host "   $ver" -ForegroundColor Green
} else {
    throw "ffmpeg 验证失败"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  安装完成！请重启 API 服务使配置生效" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
