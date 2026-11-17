param(
    [string]$DllPath = "src\SmartCompanions\bin\SmartCompanions.dll"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([System.IO.Path]::IsPathRooted($DllPath)) {
    $resolvedPath = [System.IO.Path]::GetFullPath($DllPath)
} elseif (Test-Path -LiteralPath (Join-Path (Get-Location).Path $DllPath)) {
    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $DllPath))
} else {
    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $DllPath))
}

if (-not (Test-Path -LiteralPath $resolvedPath)) {
    throw "Target DLL not found: $resolvedPath"
}

$cecilPath = "G:\Steam\steamapps\common\ZED ZONE\BepInEx\core\Mono.Cecil.dll"
if (-not (Test-Path -LiteralPath $cecilPath)) {
    throw "Mono.Cecil.dll not found at: $cecilPath"
}

[System.Reflection.Assembly]::LoadFrom($cecilPath) | Out-Null

$zeroVersion = [Version]::new(0, 0, 0, 0)
$inBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$msIn = [System.IO.MemoryStream]::new($inBytes, $false)
$changed = $false
$prevVersion = $null

try {
    $asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($msIn)
    try {
        $acRef = $asm.MainModule.AssemblyReferences | Where-Object { $_.Name -eq "Assembly-CSharp" } | Select-Object -First 1
        if ($null -eq $acRef) {
            throw "Assembly-CSharp AssemblyRef not found in: $resolvedPath"
        }
        $prevVersion = $acRef.Version.ToString()
        if ($acRef.Version -ne $zeroVersion) {
            $acRef.Version = $zeroVersion
            $changed = $true
            $wp = [Mono.Cecil.WriterParameters]::new()
            $wp.DeterministicMvid = $true
            $wp.Timestamp = [uint32]0
            $msOut = [System.IO.MemoryStream]::new()
            try {
                $asm.Write($msOut, $wp)
                $outBytes = $msOut.ToArray()
            } finally {
                $msOut.Dispose()
            }
        }
    } finally {
        $asm.Dispose()
    }
} finally {
    $msIn.Dispose()
}

if ($changed) {
    [System.IO.File]::WriteAllBytes($resolvedPath, $outBytes)
}

$finalBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$sha256 = [BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData($finalBytes)).Replace("-", "").ToLowerInvariant()

$msVerify = [System.IO.MemoryStream]::new($finalBytes, $false)
try {
    $verifyAsm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($msVerify)
    try {
        $verifiedRef = $verifyAsm.MainModule.AssemblyReferences | Where-Object { $_.Name -eq "Assembly-CSharp" } | Select-Object -First 1
        if ($null -eq $verifiedRef -or $verifiedRef.Version -ne $zeroVersion) {
            throw "Verification failed: Assembly-CSharp AssemblyRef version is $($verifiedRef.Version)"
        }
        $confirmedVersion = $verifiedRef.Version.ToString()
    } finally {
        $verifyAsm.Dispose()
    }
} finally {
    $msVerify.Dispose()
}

Write-Host "Normalized DLL: $resolvedPath (rewritten=$changed, previous=$prevVersion)"
Write-Host "Confirmed Assembly-CSharp AssemblyRef Version: $confirmedVersion"
Write-Host "SHA256: $sha256"
