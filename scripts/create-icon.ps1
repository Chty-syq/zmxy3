$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskAssets=Join-Path $PSScriptRoot '../src/ZmxyDemo/app.ico'
$taskImages=@()
foreach($taskSize in @(16,32,48,256)) {
    $taskBitmap=New-Object Drawing.Bitmap($taskSize,$taskSize)
    $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
    $taskGraphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $taskGraphics.ScaleTransform($taskSize/40.0,$taskSize/40.0)
    $taskShape=New-Object Drawing.Drawing2D.GraphicsPath
    $taskShape.AddArc(1,1,20,20,180,90)
    $taskShape.AddArc(19,1,20,20,270,90)
    $taskShape.AddArc(19,19,20,20,0,90)
    $taskShape.AddArc(1,19,20,20,90,90)
    $taskShape.CloseFigure()
    $taskBrush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(38,113,88))
    $taskGraphics.FillPath($taskBrush,$taskShape)
    $taskPen=New-Object Drawing.Pen([Drawing.Color]::FromArgb(241,249,237),2.2)
    $taskPen.StartCap=$taskPen.EndCap=[Drawing.Drawing2D.LineCap]::Round
    $taskPen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
    $taskPoints=[Drawing.PointF[]]@([Drawing.PointF]::new(9,27),[Drawing.PointF]::new(18,14),[Drawing.PointF]::new(23,21),[Drawing.PointF]::new(27,17),[Drawing.PointF]::new(32,27))
    $taskGraphics.DrawLines($taskPen,$taskPoints)
    $taskGraphics.DrawLine($taskPen,13,29,28,29)
    $taskGraphics.DrawArc($taskPen,26,8,6,6,30,270)
    $taskStream=New-Object IO.MemoryStream
    $taskBitmap.Save($taskStream,[Drawing.Imaging.ImageFormat]::Png)
    $taskImages+=,[pscustomobject]@{Size=$taskSize;Bytes=$taskStream.ToArray()}
    $taskStream.Dispose();$taskGraphics.Dispose();$taskShape.Dispose();$taskBrush.Dispose();$taskPen.Dispose();$taskBitmap.Dispose()
}
$taskOutput=[IO.File]::Create([IO.Path]::GetFullPath($taskAssets))
$taskWriter=New-Object IO.BinaryWriter($taskOutput)
try {
    $taskWriter.Write([uint16]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]$taskImages.Count)
    $taskOffset=6+16*$taskImages.Count
    foreach($taskImage in $taskImages) {
        $taskDimension=if($taskImage.Size -eq 256){0}else{$taskImage.Size}
        $taskWriter.Write([byte]$taskDimension);$taskWriter.Write([byte]$taskDimension)
        $taskWriter.Write([byte]0);$taskWriter.Write([byte]0);$taskWriter.Write([uint16]1);$taskWriter.Write([uint16]32)
        $taskWriter.Write([uint32]$taskImage.Bytes.Length);$taskWriter.Write([uint32]$taskOffset)
        $taskOffset+=$taskImage.Bytes.Length
    }
    foreach($taskImage in $taskImages){$taskWriter.Write([byte[]]$taskImage.Bytes)}
} finally {$taskWriter.Dispose();$taskOutput.Dispose()}
