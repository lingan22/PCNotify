# scripts/generate-icon.ps1
# Generates high-quality Windows multi-resolution app.ico and app.png for PCNotify
# Design: Option 2 - Windows Terminal / PowerToys Style Stacked Dual Panes

Add-Type -AssemblyName System.Drawing

$assetsDir = Join-Path $PSScriptRoot "..\src\PCMonitor\Assets"
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
}

$icoPath = Join-Path $assetsDir "app.ico"
$pngPath = Join-Path $assetsDir "app.png"

function Create-RoundedRectPath([System.Drawing.RectangleF]$rect, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2.0
    if ($d -gt $rect.Width) { $d = $rect.Width }
    if ($d -gt $rect.Height) { $d = $rect.Height }
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-PCNotifyGraphic {
    param(
        [int]$size
    )

    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # 1. 基础深色外框 (Squircle Base)
    $pad = [float][Math]::Max(0.5, $size * 0.05)
    $w = [float]($size - (2.0 * $pad))
    $radius = [float][Math]::Max(2.0, $size * 0.22)
    $baseRect = New-Object System.Drawing.RectangleF($pad, $pad, $w, $w)

    $basePath = Create-RoundedRectPath $baseRect $radius
    $baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 17, 23)) # #0F1117
    $g.FillPath($baseBrush, $basePath)
    $baseBrush.Dispose()

    # 外框微弱轮廓线 (保证在黑底/深色任务栏上的边界分明)
    $penWidth = [float][Math]::Max(1.0, $size * 0.015)
    $basePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 38, 42, 54), $penWidth)
    $g.DrawPath($basePen, $basePath)
    $basePen.Dispose()
    $basePath.Dispose()

    $cx = [float]($size / 2.0)
    $cy = [float]($size / 2.0)

    # 2. 后层窗格 (主系统桌面背景窗口)
    $backW = [float]($size * 0.39)
    $backH = [float]($size * 0.31)
    $backX = [float]($cx - ($size * 0.25))
    $backY = [float]($cy - ($size * 0.25))
    $backCorner = [float][Math]::Max(1.5, $size * 0.035)

    $backRect = New-Object System.Drawing.RectangleF($backX, $backY, $backW, $backH)
    $backPath = Create-RoundedRectPath $backRect $backCorner
    $backBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 30, 42))
    $g.FillPath($backBrush, $backPath)
    $backBrush.Dispose()

    $backPenWidth = [float][Math]::Max(1.0, $size * 0.012)
    $backPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 60, 75, 100), $backPenWidth)
    $g.DrawPath($backPen, $backPath)
    $backPen.Dispose()
    $backPath.Dispose()

    # 3. 前层窗格 (右下高亮通知卡片 - 经典微软蓝渐变)
    $frontW = [float]($size * 0.39)
    $frontH = [float]($size * 0.31)
    $frontX = [float]($cx - ($size * 0.14))
    $frontY = [float]($cy - ($size * 0.06))
    $frontCorner = [float][Math]::Max(1.5, $size * 0.035)

    $frontRect = New-Object System.Drawing.RectangleF($frontX, $frontY, $frontW, $frontH)
    $frontPath = Create-RoundedRectPath $frontRect $frontCorner

    $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $frontRect,
        [System.Drawing.Color]::FromArgb(255, 0, 120, 215),  # Windows Blue #0078D7
        [System.Drawing.Color]::FromArgb(255, 0, 90, 180),   # Windows Deep Blue #005AB4
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $g.FillPath($gradBrush, $frontPath)
    $gradBrush.Dispose()
    $frontPath.Dispose()

    # 4. 前层窗格内的纯白极简通知条纹
    $lineThickness = [float][Math]::Max(1.2, $size * 0.02)
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $lineThickness)
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $line1Y = [float]($frontRect.Y + ($frontRect.Height * 0.38))
    $line1X1 = [float]($frontRect.X + ($frontRect.Width * 0.18))
    $line1X2 = [float]($frontRect.X + ($frontRect.Width * 0.68))
    $g.DrawLine($linePen, $line1X1, $line1Y, $line1X2, $line1Y)

    $line2Y = [float]($frontRect.Y + ($frontRect.Height * 0.62))
    $line2X1 = [float]($frontRect.X + ($frontRect.Width * 0.18))
    $line2X2 = [float]($frontRect.X + ($frontRect.Width * 0.46))
    $g.DrawLine($linePen, $line2X1, $line2Y, $line2X2, $line2Y)

    $linePen.Dispose()
    $g.Dispose()

    return $bmp
}

# 输出高分辨率大图 app.png
$bigBmp = Draw-PCNotifyGraphic 256
$bigBmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bigBmp.Dispose()

# 构建 multi-resolution .ico (16, 24, 32, 48, 64, 128, 256)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entryData = @()

foreach ($s in $sizes) {
    $b = Draw-PCNotifyGraphic $s

    if ($s -le 128) {
        # 使用标准 DIB (BITMAPINFOHEADER + BGRA + 1bpp MASK)
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $ms

        $bw.Write([uint32]40)
        $bw.Write([int32]$s)
        $bw.Write([int32]($s * 2)) # Height is doubled in ICO DIB
        $bw.Write([uint16]1)      # biPlanes
        $bw.Write([uint16]32)     # biBitCount
        $bw.Write([uint32]0)      # biCompression (BI_RGB)
        $bw.Write([uint32]($s * $s * 4)) # biSizeImage
        $bw.Write([int32]0)
        $bw.Write([int32]0)
        $bw.Write([uint32]0)
        $bw.Write([uint32]0)

        # 像素数据: 从底向上读取 (Bottom-Up)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $pixel = $b.GetPixel($x, $y)
                $bw.Write([byte]$pixel.B)
                $bw.Write([byte]$pixel.G)
                $bw.Write([byte]$pixel.R)
                $bw.Write([byte]$pixel.A)
            }
        }

        # 1-bit mask (全 0 表示由 32 位 Alpha 通道控制透明度)
        $maskRowBytes = [int][Math]::Ceiling($s / 32.0) * 4
        $maskRow = New-Object byte[] $maskRowBytes
        for ($y = 0; $y -lt $s; $y++) {
            $bw.Write($maskRow)
        }

        $bw.Flush()
        $entryData += ,$ms.ToArray()
        $bw.Close()
        $ms.Close()
    }
    else {
        # 256x256 采用标准 PNG 格式
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entryData += ,$ms.ToArray()
        $ms.Close()
    }

    $b.Dispose()
}

# 写入 .ico 文件
$icoStream = [System.IO.File]::Create($icoPath)
$icoWriter = New-Object System.IO.BinaryWriter $icoStream

$icoWriter.Write([uint16]0) # Reserved
$icoWriter.Write([uint16]1) # Type = 1 (Icon)
$icoWriter.Write([uint16]$sizes.Length)

$offset = 6 + ($sizes.Length * 16)
for ($i = 0; $i -lt $sizes.Length; $i++) {
    $s = $sizes[$i]
    $bytes = $entryData[$i]
    $w = if ($s -ge 256) { 0 } else { $s }
    $h = if ($s -ge 256) { 0 } else { $s }

    $icoWriter.Write([byte]$w)
    $icoWriter.Write([byte]$h)
    $icoWriter.Write([byte]0)   # Color count
    $icoWriter.Write([byte]0)   # Reserved
    $icoWriter.Write([uint16]1) # Planes
    $icoWriter.Write([uint16]32)# BitCount
    $icoWriter.Write([uint32]$bytes.Length)
    $icoWriter.Write([uint32]$offset)

    $offset += $bytes.Length
}

foreach ($bytes in $entryData) {
    $icoWriter.Write($bytes)
}

$icoWriter.Close()
$icoStream.Close()

Write-Host "New Option 2 Windows-style icon generated successfully at: $icoPath"
