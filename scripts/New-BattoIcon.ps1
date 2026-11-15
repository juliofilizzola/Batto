<#
.SYNOPSIS
    Generates the static Batto application icon (src/Batto/Assets/batto.ico).
    The design is the segmented ring used in the tray, drawn with three sample levels.
#>
param(
    [string]$OutPath = (Join-Path $PSScriptRoot '..\src\Batto\Assets\batto.ico')
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 256
$levels = 85, 35, 12
$colors = @(
    [System.Drawing.Color]::FromArgb(34, 197, 94),
    [System.Drawing.Color]::FromArgb(245, 158, 11),
    [System.Drawing.Color]::FromArgb(239, 68, 68)
)
$gap = 12.0
$count = $levels.Count
$segment = (360.0 - $gap * $count) / $count

function New-RingPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap -ArgumentList $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $stroke = [float][Math]::Max(2.0, $size * 0.16)
    $inset = [float]($stroke / 2 + 0.5)
    $box = New-Object System.Drawing.RectangleF -ArgumentList $inset, $inset, ([float]($size - 2 * $inset)), ([float]($size - 2 * $inset))

    $trackPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(255, 58, 58, 58)), $stroke
    for ($i = 0; $i -lt $count; $i++) {
        $start = [float](-90.0 + $gap / 2 + $i * ($segment + $gap))
        $g.DrawArc($trackPen, $box, $start, [float]$segment)

        $pen = New-Object System.Drawing.Pen -ArgumentList $colors[$i], $stroke
        $g.DrawArc($pen, $box, $start, [float]($segment * $levels[$i] / 100))
        $pen.Dispose()
    }
    $trackPen.Dispose()
    $g.Dispose()
    return $bmp
}

$entries = foreach ($s in $sizes) {
    $bmp = New-RingPng $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
}

# ICO container with PNG-encoded images (supported on Windows Vista and later)
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter -ArgumentList $out
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$entries.Count)

$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$e.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) {
    $bw.Write($e.Data)
}
$bw.Flush()

$dest = [System.IO.Path]::GetFullPath($OutPath)
[System.IO.File]::WriteAllBytes($dest, $out.ToArray())
Write-Host "Icone gerado em $dest"
