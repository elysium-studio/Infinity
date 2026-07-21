param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$InputDirectory,
    [string]$OutputDirectory = "",
    [string]$ProductId = $env:INFINITY_STORE_PRODUCT_ID,
    [string]$IdentityName = $env:INFINITY_STORE_IDENTITY_NAME,
    [string]$Publisher = $env:INFINITY_STORE_PUBLISHER,
    [string]$PublisherDisplayName = $env:INFINITY_STORE_PUBLISHER_DISPLAY_NAME,
    [string]$FlightId = "",
    [switch]$NoCommit,
    [switch]$PackageOnly
)

$ErrorActionPreference = "Stop"
$repositoryPath = Split-Path $PSScriptRoot -Parent
$manifestTemplatePath = Join-Path $repositoryPath "Store\Package.appxmanifest.template"
$logoPath = Join-Path $repositoryPath "Infinity.Shell.WinUI\Assets\Infinity.png"

if ([string]::IsNullOrWhiteSpace($OutputDirectory))
{
    $OutputDirectory = Join-Path $repositoryPath "Publish\$Version\Store"
}

function Assert-Value
{
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value))
    {
        throw "$Name is required"
    }
}

function Get-MakeAppxPath
{
    $windowsKitsPath = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $path = Get-ChildItem $windowsKitsPath -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\makeappx.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $path)
    {
        throw "MakeAppx.exe was not found in the Windows SDK"
    }

    return $path
}

function Convert-ToXmlValue
{
    param(
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-StoreImage
{
    param(
        [string]$Source,
        [string]$Destination,
        [int]$Width,
        [int]$Height
    )

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($Source)

    try
    {
        $destinationImage = New-Object System.Drawing.Bitmap($Width, $Height)

        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($destinationImage)

            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $scale = [Math]::Min($Width / $sourceImage.Width, $Height / $sourceImage.Height)
                $drawWidth = [int][Math]::Round($sourceImage.Width * $scale)
                $drawHeight = [int][Math]::Round($sourceImage.Height * $scale)
                $drawX = [int](($Width - $drawWidth) / 2)
                $drawY = [int](($Height - $drawHeight) / 2)
                $graphics.DrawImage($sourceImage, $drawX, $drawY, $drawWidth, $drawHeight)
            }
            finally
            {
                $graphics.Dispose()
            }

            $destinationImage.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $destinationImage.Dispose()
        }
    }
    finally
    {
        $sourceImage.Dispose()
    }
}

Assert-Value "InputDirectory" $InputDirectory
Assert-Value "IdentityName or INFINITY_STORE_IDENTITY_NAME" $IdentityName
Assert-Value "Publisher or INFINITY_STORE_PUBLISHER" $Publisher
Assert-Value "PublisherDisplayName or INFINITY_STORE_PUBLISHER_DISPLAY_NAME" $PublisherDisplayName

if (-not $PackageOnly)
{
    Assert-Value "ProductId or INFINITY_STORE_PRODUCT_ID" $ProductId
}

if (-not (Test-Path $InputDirectory))
{
    throw "Published application directory was not found: $InputDirectory"
}

if (-not (Test-Path (Join-Path $InputDirectory "Infinity.exe")))
{
    throw "Infinity.exe was not found in $InputDirectory"
}

$numericVersion = $Version -replace '-.*$', ''

if ($numericVersion -notmatch '^\d+\.\d+\.\d+$')
{
    throw "Version must use major.minor.patch format with an optional prerelease suffix"
}

$packageVersion = "$numericVersion.0"
$stagingPath = Join-Path $OutputDirectory "Staging"
$packagePath = Join-Path $OutputDirectory "Infinity-$Version.msix"
$symbolsPath = Join-Path $OutputDirectory "Symbols"
$appxSymbolsPath = Join-Path $OutputDirectory "Infinity-$Version.appxsym"
$uploadStagingPath = Join-Path $OutputDirectory "Upload"
$uploadPath = Join-Path $OutputDirectory "Infinity-$Version.msixupload"

New-Item $OutputDirectory -ItemType Directory -Force | Out-Null

if (Test-Path $stagingPath)
{
    Remove-Item $stagingPath -Recurse -Force
}

if (Test-Path $packagePath)
{
    Remove-Item $packagePath -Force
}

foreach ($path in @($symbolsPath, $uploadStagingPath))
{
    if (Test-Path $path)
    {
        Remove-Item $path -Recurse -Force
    }
}

foreach ($path in @($appxSymbolsPath, $uploadPath))
{
    if (Test-Path $path)
    {
        Remove-Item $path -Force
    }
}

New-Item $stagingPath -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $InputDirectory "*") $stagingPath -Recurse -Force

$symbolFiles = @(Get-ChildItem $stagingPath -Filter "*.pdb" -File -Recurse)

if ($symbolFiles.Count -gt 0)
{
    New-Item $symbolsPath -ItemType Directory -Force | Out-Null

    foreach ($symbolFile in $symbolFiles)
    {
        Copy-Item $symbolFile.FullName (Join-Path $symbolsPath $symbolFile.Name) -Force
        Remove-Item $symbolFile.FullName -Force
    }
}

$storeAssetsPath = Join-Path $stagingPath "StoreAssets"
New-Item $storeAssetsPath -ItemType Directory -Force | Out-Null
New-StoreImage $logoPath (Join-Path $storeAssetsPath "StoreLogo.png") 50 50
New-StoreImage $logoPath (Join-Path $storeAssetsPath "Square44x44Logo.png") 44 44
New-StoreImage $logoPath (Join-Path $storeAssetsPath "Square150x150Logo.png") 150 150

$manifest = Get-Content $manifestTemplatePath -Raw
$manifest = $manifest.Replace("__IDENTITY_NAME__", (Convert-ToXmlValue $IdentityName))
$manifest = $manifest.Replace("__PUBLISHER__", (Convert-ToXmlValue $Publisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", (Convert-ToXmlValue $PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $packageVersion)
[System.IO.File]::WriteAllText((Join-Path $stagingPath "AppxManifest.xml"), $manifest,
    [System.Text.UTF8Encoding]::new($false))

$makeAppxPath = Get-MakeAppxPath
& $makeAppxPath pack /d $stagingPath /p $packagePath /o

if ($LASTEXITCODE -ne 0)
{
    throw "Microsoft Store package creation failed with exit code $LASTEXITCODE"
}

if ($symbolFiles.Count -gt 0)
{
    $symbolsArchivePath = "$appxSymbolsPath.zip"
    Compress-Archive -Path (Join-Path $symbolsPath "*") -DestinationPath $symbolsArchivePath -CompressionLevel Optimal -Force
    Move-Item $symbolsArchivePath $appxSymbolsPath -Force
}

New-Item $uploadStagingPath -ItemType Directory -Force | Out-Null
Copy-Item $packagePath $uploadStagingPath

if (Test-Path $appxSymbolsPath)
{
    Copy-Item $appxSymbolsPath $uploadStagingPath
}

$uploadArchivePath = "$uploadPath.zip"
Compress-Archive -Path (Join-Path $uploadStagingPath "*") -DestinationPath $uploadArchivePath -CompressionLevel NoCompression -Force
Move-Item $uploadArchivePath $uploadPath -Force

Write-Host "Microsoft Store package created: $uploadPath" -ForegroundColor Green

if ($PackageOnly)
{
    exit 0
}

$msstore = Get-Command msstore -ErrorAction SilentlyContinue

if (-not $msstore)
{
    throw "Microsoft Store Developer CLI was not found"
}

$tenantId = $env:INFINITY_STORE_TENANT_ID
$sellerId = $env:INFINITY_STORE_SELLER_ID
$clientId = $env:INFINITY_STORE_CLIENT_ID
$clientSecret = $env:INFINITY_STORE_CLIENT_SECRET

if ($tenantId -and $sellerId -and $clientId -and $clientSecret)
{
    & $msstore.Source reconfigure --tenantId $tenantId --sellerId $sellerId --clientId $clientId --clientSecret $clientSecret

    if ($LASTEXITCODE -ne 0)
    {
        throw "Microsoft Store Developer CLI authentication failed with exit code $LASTEXITCODE"
    }
}

$publishArguments = @(
    "publish"
    $repositoryPath
    "--inputFile", $uploadPath
    "--appId", $ProductId
)

if ($NoCommit)
{
    $publishArguments += "--noCommit"
}

if (-not [string]::IsNullOrWhiteSpace($FlightId))
{
    $publishArguments += @("--flightId", $FlightId)
}

& $msstore.Source @publishArguments

if ($LASTEXITCODE -ne 0)
{
    throw "Microsoft Store submission failed with exit code $LASTEXITCODE"
}

Write-Host "Microsoft Store submission completed for $ProductId" -ForegroundColor Green
