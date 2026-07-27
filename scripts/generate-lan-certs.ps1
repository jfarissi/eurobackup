# Génère un certificat TLS pour le LAN (euro13 / localhost).
# Usage (PowerShell, depuis la racine Backup.Web.Api) :
#   .\scripts\generate-lan-certs.ps1
#   .\scripts\generate-lan-certs.ps1 -HostName euro13
#
# Puis importer certs\euro13.crt dans « Autorités de certification racines de confiance »
# (utilisateur ou machine) pour que Chrome n'affiche plus « connexion non sécurisée ».

param(
    [string]$HostName = "euro13",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

if (-not $OutDir) {
    $OutDir = Join-Path (Split-Path $PSScriptRoot -Parent) "certs"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$certPath = Join-Path $OutDir "$HostName.crt"
$keyPath = Join-Path $OutDir "$HostName.key"
$pfxPath = Join-Path $OutDir "$HostName.pfx"

# Certificat auto-signé avec SAN (DNS + localhost)
$cert = New-SelfSignedCertificate `
    -DnsName $HostName, "localhost", "127.0.0.1" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5) `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -FriendlyName "Backup LAN $HostName"

$pwd = ConvertTo-SecureString -String "backup" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pwd | Out-Null

# Export PEM (crt + key) via openssl si dispo, sinon OpenSSL .NET / certutil workaround
$openssl = Get-Command openssl -ErrorAction SilentlyContinue
if ($openssl) {
    & openssl pkcs12 -in $pfxPath -clcerts -nokeys -out $certPath -passin pass:backup
    & openssl pkcs12 -in $pfxPath -nocerts -nodes -out $keyPath -passin pass:backup
} else {
    # Fallback: exporter le .cer public ; la clé pour nginx nécessite openssl
    Export-Certificate -Cert $cert -FilePath $certPath -Type CERT | Out-Null
    Write-Warning "OpenSSL introuvable. Installez OpenSSL ou Git for Windows, puis relancez pour générer le .key PEM."
    Write-Host "PFX généré : $pfxPath (mot de passe: backup)"
    Write-Host "Pour nginx il faut un .key PEM. Exemple Git Bash :"
    Write-Host "  openssl pkcs12 -in certs/$HostName.pfx -clcerts -nokeys -out certs/$HostName.crt -passin pass:backup"
    Write-Host "  openssl pkcs12 -in certs/$HostName.pfx -nocerts -nodes -out certs/$HostName.key -passin pass:backup"
    exit 0
}

# Nettoyage store utilisateur (optionnel : on laisse le cert pour import facile)
Write-Host "Certificats générés :"
Write-Host "  $certPath"
Write-Host "  $keyPath"
Write-Host ""
Write-Host "1) docker compose up -d nginx"
Write-Host "2) Ouvrir https://$HostName"
Write-Host "3) Pour supprimer l'avertissement Chrome : double-clic sur $certPath"
Write-Host "   → Installer → Ordinateur local → Autorités de certification racines de confiance"
