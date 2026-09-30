Add-Type -AssemblyName System.Drawing

$assetPath = Join-Path $PSScriptRoot '..\src\KeyFix.App\Assets\KeyFix.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $inset = [Math]::Max(1.0, $size * 0.06)
    $bounds = [System.Drawing.RectangleF]::new($inset, $inset, $size - (2 * $inset), $size - (2 * $inset))
    $radius = $size * 0.22
    $diameter = $radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($bounds.Left, $bounds.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bounds.Left, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $tile = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(29, 33, 30))
    $graphics.FillPath($tile, $path)

    $fontSize = $size * 0.52
    $font = [System.Drawing.Font]::new('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $cream = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(247, 244, 236))
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textBounds = [System.Drawing.RectangleF]::new(0, -($size * 0.025), $size * 0.92, $size)
    $graphics.DrawString('K', $font, $cream, $textBounds, $format)

    $dot = [Math]::Max(2.0, $size * 0.115)
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(197, 104, 66))
    $graphics.FillEllipse($accent, $size * 0.70, $size * 0.70, $dot, $dot)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())

    $stream.Dispose()
    $accent.Dispose()
    $format.Dispose()
    $cream.Dispose()
    $font.Dispose()
    $tile.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$output = [System.IO.File]::Create($assetPath)
$writer = [System.IO.BinaryWriter]::new($output)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)

$offset = 6 + (16 * $images.Count)
for ($index = 0; $index -lt $images.Count; $index++) {
    $size = $sizes[$index]
    $dimension = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Dispose()
$output.Dispose()
Write-Output $assetPath
