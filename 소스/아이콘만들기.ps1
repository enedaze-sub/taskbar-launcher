$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(16,23,34))
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(109,224,212))
foreach ($x in @(46,142)) {
    foreach ($y in @(46,142)) {
        $g.FillRectangle($brush,$x,$y,68,68)
    }
}
$memory = New-Object System.IO.MemoryStream
$bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
$png = $memory.ToArray()
$file = [System.IO.File]::Create((Join-Path $PSScriptRoot 'launcher.ico'))
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22); $writer.Write($png)
$writer.Dispose(); $memory.Dispose(); $g.Dispose(); $brush.Dispose(); $bitmap.Dispose()
