# Generates assets\paper.ico: a cute little newspaper with a face, rendered at every standard icon size.
# Usage: pwsh tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'assets\paper.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

$paper  = [System.Drawing.Color]::FromArgb(255, 252, 247, 234)
$back   = [System.Drawing.Color]::FromArgb(255, 226, 214, 190)
$ink    = [System.Drawing.Color]::FromArgb(255, 72, 56, 46)
$gray   = [System.Drawing.Color]::FromArgb(255, 196, 182, 160)
$cheek  = [System.Drawing.Color]::FromArgb(210, 246, 150, 150)
$shine  = [System.Drawing.Color]::White

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

# Draws in a 32-unit design grid scaled to $size; small sizes get a simplified layout so lines stay crisp.
function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'Half'
    $s = $size / 32.0
    $g.ScaleTransform($s, $s)
    $detailed = $size -ge 32
    $tiny = $size -le 20
    $stroke = if ($tiny) { 1.7 } elseif ($size -le 32) { 1.3 } else { 1.1 }

    $inkPen = New-Object System.Drawing.Pen $ink, $stroke
    $inkPen.LineJoin = 'Round'
    $inkBrush = New-Object System.Drawing.SolidBrush $ink

    # Back page, tilted a little for a playful look.
    $state = $g.Save()
    $g.TranslateTransform(19, 15); $g.RotateTransform(12); $g.TranslateTransform(-19, -15)
    $bp = RoundRect 9 3 19 24 3
    $g.FillPath((New-Object System.Drawing.SolidBrush $back), $bp)
    $g.DrawPath($inkPen, $bp)
    if ($detailed) {
        $grayPen = New-Object System.Drawing.Pen $ink, 1.0
        $grayPen.StartCap = $grayPen.EndCap = 'Round'
        foreach ($y in 8, 11, 14) { $g.DrawLine($grayPen, 22, $y, 25.5, $y) }
    }
    $g.Restore($state)

    # Front page.
    $fp = RoundRect 2.5 5 22 25 4
    $g.FillPath((New-Object System.Drawing.SolidBrush $paper), $fp)
    $g.DrawPath($inkPen, $fp)

    # Masthead (headline bar) as the "hair".
    $mh = RoundRect 5.5 8 16 3.6 1.4
    $g.FillPath($inkBrush, $mh)
    if ($detailed) {
        $linePen = New-Object System.Drawing.Pen $gray, 1.0
        $linePen.StartCap = $linePen.EndCap = 'Round'
        $g.DrawLine($linePen, 6.5, 13.6, 12, 13.6)
        $g.DrawLine($linePen, 15, 13.6, 20.5, 13.6)
    }

    # Face.
    $cx = 13.5
    if (-not $tiny) {
        $cheekBrush = New-Object System.Drawing.SolidBrush $cheek
        $g.FillEllipse($cheekBrush, 5.6, 21.0, 3.8, 2.4)
        $g.FillEllipse($cheekBrush, 17.6, 21.0, 3.8, 2.4)
    }
    $eyeW = if ($tiny) { 3.8 } else { 2.8 }
    $eyeH = if ($tiny) { 4.4 } else { 3.6 }
    foreach ($ex in ($cx - 3.6), ($cx + 3.6)) {
        $g.FillEllipse($inkBrush, $ex - $eyeW / 2, 16.6, $eyeW, $eyeH)
        if ($detailed) {
            $g.FillEllipse((New-Object System.Drawing.SolidBrush $shine), $ex - 0.9, 17.1, 1.2, 1.2)
        }
    }
    $smile = New-Object System.Drawing.Pen $ink, ($stroke * 1.05)
    $smile.StartCap = $smile.EndCap = 'Round'
    if ($tiny) { $g.DrawArc($smile, $cx - 3, 20, 6, 4.4, 20, 140) }
    else { $g.DrawArc($smile, $cx - 2.2, 19.6, 4.4, 3.6, 25, 130) }

    $g.Dispose()
    $bmp
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) {
    $bmp = Render $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($sz -eq 256) { $bmp.Save((Join-Path $root 'assets\paper-preview.png')) }
    $bmp.Dispose()
    , $ms.ToArray()
}

$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Dispose()
"Wrote $out"
