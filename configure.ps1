# Use Steam's uninstall entry for Rimworld to find its install location.
$rimworldSearchDir = Get-ItemPropertyValue -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 294100" -Name InstallLocation
if((Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 294100") -eq $false) {
    Write-Error ("Rimworld install location not detected from Steam registry entry.")
    exit
}

$rimworldDataDir = Join-Path -Path $rimworldSearchDir -ChildPath "RimWorldWin64_Data"
$rimworldManagedDir = Join-Path -Path $rimworldDataDir -ChildPath "Managed"
$assemblyPath = Join-Path -Path $rimworldManagedDir -ChildPath "Assembly-CSharp.dll"

# Make sure Assembly-CSharp exists.
if((Test-Path $assemblyPath) -eq $false) {
    Write-Error ("Assembly file not found in: " + $assemblyPath)
    exit
}
Write-Host ("Assembly file found at: " + $assemblyPath)

# Determine the major-minor version of the current rimworld install.
$productVersion = Get-ChildItem -Path $assemblyPath | Select-Object -ExpandProperty VersionInfo | Select-Object -ExpandProperty ProductVersion
$majorMinorSplit = $productVersion -split "\."
$majorMinorVersion = $majorMinorSplit[0..1] -join "."
Write-Host ("Detected Version: " + $majorMinorVersion)

# Make folder for IlSpy and its decompiler.
$dependencyFolder = Join-Path -Path $PSScriptRoot -ChildPath "Dependencies"
$ilSpyFolder = Join-Path -Path $dependencyFolder -ChildPath "ILSpy"
if ((Test-Path -Path $ilSpyFolder) -eq $false) {
    $null = New-Item -Path $ilSpyFolder -ItemType Directory
}

# Download ILSpy into ./Dependencies
$ilSpyUri = "https://github.com/cseelhoff/ILSpy/releases/download/7.1b/ICSharpCode.Decompiler.zip"
$ilSpyZip = Join-Path -Path $dependencyFolder -ChildPath "ICSharpCode.Decompiler.zip"
Invoke-WebRequest -Uri $ilSpyUri -OutFile $ilSpyZip
Expand-Archive -Path $ilSpyZip -DestinationPath $ilSpyFolder -Force

# Make folder for decompiled RimWorld sources.
$rimworldSourceFolder = Join-Path -Path $dependencyFolder -ChildPath "RimWorld"
if ((Test-Path -Path $rimworldSourceFolder) -eq $false) {
    $null = New-Item -Path $rimworldSourceFolder -ItemType Directory
}

# Load assemblies for running PortablePdbWriter.
$null = [Reflection.Assembly]::LoadFile((Join-Path -Path $ilSpyFolder -ChildPath "System.Collections.Immutable.dll"))
$null = [Reflection.Assembly]::LoadFile((Join-Path -Path $ilSpyFolder -ChildPath "System.Reflection.Metadata.dll"))
$null = [Reflection.Assembly]::LoadFile((Join-Path -Path $ilSpyFolder -ChildPath "ICSharpCode.Decompiler.dll"))

# Write PDB next to original Assembly-CSharp, and put decompiled sources in ./Dependencies/RimWorld
$pdbDestinationFile = Join-Path -Path $rimworldManagedDir -ChildPath "Assembly-CSharp.pdb"
Write-Host ("Decompiling source to: " + $rimworldSourceFolder)
Write-Host ("Writing PDB to: " + $pdbDestinationFile)
Write-Host ("This process could take several minutes. Please wait...")
[ICSharpCode.Decompiler.DebugInfo.PortablePdbWriter]::WritePdb($assemblyPath, $pdbDestinationFile, $rimworldSourceFolder)
