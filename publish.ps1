# 配布用の zip を作る。
#   powershell -ExecutionPolicy Bypass -File publish.ps1
#
# 毎回 dist\<名前>\ を空にしてから publish するので、
# 削除・名前変更した who ファイルが残ることはない。
# 名前とバージョンは csproj の AssemblyName・Version から取る。
# できるもの:
#   dist\<名前>\                    配布フォルダ（exe・settings.json・who・README.md）。手元での動作確認用
#   dist\<名前>-<バージョン>.zip      上のフォルダから who を除いて zip にしたもの。GitHub のリリースに添付する用
#                                    （リリースは誰でもダウンロードできるので、メンバーの名前が入った who は入れない）

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'Whiteboard.csproj'
$distDir = Join-Path $PSScriptRoot 'dist'

$appName = (dotnet msbuild $project -getProperty:AssemblyName).Trim()
$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $appName -or -not $version) { throw '名前またはバージョンを取得できませんでした。' }
# アイコンはリポジトリに含めていないので、手元に無ければアイコンなしの exe になる
if (-not (Test-Path 'Assets\app.ico')) { Write-Warning 'Assets\app.ico が無いので、アイコンなしで publish します。' }
$outDir = Join-Path $distDir $appName
$zipPath = Join-Path $distDir "$appName-$version.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
New-Item -ItemType Directory -Force $outDir | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $outDir
if ($LASTEXITCODE -ne 0) { throw 'publish に失敗しました。' }

# zip の中は「<名前>/」フォルダ1つにまとめる（展開したときに散らばらないように）。
# Windows PowerShell 5.1 の ZipFile.CreateFromDirectory は区切りを「\」で書いてしまうので、
# zip の仕様どおり「/」で1ファイルずつ追加する
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $outDir -Recurse -File | Where-Object {
        -not $_.FullName.StartsWith((Join-Path $outDir 'who') + '\', [System.StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object {
        $entryName = "$appName/" + $_.FullName.Substring($outDir.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host ''
Write-Host "配布フォルダ: $outDir"
Get-ChildItem $outDir -Recurse -File | ForEach-Object { Write-Host ('  ' + $_.FullName.Substring($outDir.Length + 1)) }
Write-Host "zip:         $zipPath（who を除く）"
