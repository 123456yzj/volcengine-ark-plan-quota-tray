# Fetch pinned official distribution assets. No Node, npm, SDK or credentials.
param([string]$Destination = 'runtime-bootstrap')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'repository root missing' }
if ([System.IO.Path]::IsPathRooted($Destination)) { $target = $Destination }
else { $target = Join-Path $root $Destination }
$version = '1.0.37'
$digests = @{
    amd64 = '86d640ffccafa3ca5562536f226a7aadfbb362566741c1ea7e3a6a536fb58c05'
    arm64 = '975fc57ab3ec093060a1772c273b53090b0fc45a07207eef1763a27d5838e71e'
}
$assetIds = @{ amd64 = '598459687'; arm64 = '598464427' }
foreach ($arch in @('amd64', 'arm64')) {
    $dir = Join-Path $target $arch
    [System.IO.Directory]::CreateDirectory($dir) | Out-Null
    $exe = Join-Path $dir 'arkcli.exe'
    $part = $exe + '.part'
    if (-not (Test-Path -LiteralPath $exe)) {
        if (-not (Test-Path -LiteralPath $part)) {
            $url = "https://api.github.com/repos/volcengine/ark-cli/releases/assets/$($assetIds[$arch])"
            & curl.exe --fail --location --silent --show-error --proto '=https' --proto-redir '=https' --connect-timeout 15 --max-time 50 --retry 0 -H 'Accept: application/octet-stream' -H 'User-Agent: ArkLeft-Bootstrap' --output $part $url
            if ($LASTEXITCODE -ne 0) { throw "bootstrap download failed: $arch, exit $LASTEXITCODE" }
        }
        if ((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digests[$arch]) {
            throw "bootstrap digest mismatch: $arch"
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $part
        if ($signature.Status -ne 'Valid') { throw "bootstrap signature invalid: $arch ($($signature.Status))" }
        [System.IO.File]::Move($part, $exe)
    }
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digests[$arch]) {
        throw "bootstrap digest mismatch: $arch"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $exe
    if ($signature.Status -ne 'Valid') { throw "bootstrap signature invalid: $arch ($($signature.Status))" }
    "bootstrap=$version architecture=$arch sha256=$($digests[$arch]) signature=$($signature.Status)"
}
