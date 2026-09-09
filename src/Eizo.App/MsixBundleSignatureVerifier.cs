using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace Eizo;

internal interface IBundleSignatureVerifier
{
    BundleSignatureVerificationResult Verify(string bundlePath);
}

internal sealed record BundleSignatureVerificationResult(
    bool IsValid,
    string FailureCode,
    string? SignerSubject = null,
    string? SignerThumbprint = null,
    int? HResult = null);

internal sealed class MsixBundleSignatureVerifier : IBundleSignatureVerifier
{
    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdRevocationCheckNone = 0x00000010;
    private const int TrustENoSignature = unchecked((int)0x800B0100);
    private static readonly Guid WintrustActionGenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public BundleSignatureVerificationResult Verify(string bundlePath)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "SignatureInvalid");

        var trustResult = VerifyWindowsTrustAndRelease(bundlePath);
        if (trustResult != 0)
        {
            return new(
                false,
                trustResult == TrustENoSignature ? "SignatureMissing" : "SignatureInvalid",
                HResult: trustResult);
        }

        // WinVerifyTrust with WTD_STATEACTION_VERIFY may keep the package file open
        // until WTD_STATEACTION_CLOSE. Read the embedded PKCS#7 only after that state
        // has been closed, otherwise ZipFile.OpenRead can fail with an IOException on
        // an otherwise valid, fully downloaded MSIX bundle.
        return ReadEmbeddedSigner(bundlePath, trustResult);
    }

    private static int VerifyWindowsTrustAndRelease(string bundlePath)
    {
        var fileInfo = new WinTrustFileInfo(bundlePath);
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
        var trustData = new WinTrustData(fileInfoPointer, WtdStateActionVerify);

        try
        {
            return WinVerifyTrust(IntPtr.Zero, WintrustActionGenericVerifyV2, ref trustData);
        }
        finally
        {
            trustData.dwStateAction = WtdStateActionClose;
            var closeResult = WinVerifyTrust(IntPtr.Zero, WintrustActionGenericVerifyV2, ref trustData);
            if (closeResult != 0)
                Debug.WriteLine($"WinVerifyTrust close returned 0x{closeResult:X8}.");

            Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
            Marshal.FreeHGlobal(fileInfoPointer);
        }
    }

    private static BundleSignatureVerificationResult ReadEmbeddedSigner(
        string bundlePath,
        int trustResult)
    {
        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var signatureEntry = archive.GetEntry("AppxSignature.p7x");
            if (signatureEntry is null) return new(false, "SignatureMissing");

            using var signatureStream = signatureEntry.Open();
            using var signatureBytes = new MemoryStream();
            signatureStream.CopyTo(signatureBytes);

            var encodedSignature = signatureBytes.ToArray();
            if (encodedSignature.Length <= 4 ||
                encodedSignature[0] != (byte)'P' ||
                encodedSignature[1] != (byte)'K' ||
                encodedSignature[2] != (byte)'C' ||
                encodedSignature[3] != (byte)'X')
            {
                return new(false, "SignatureInvalid");
            }

            var signedCms = new SignedCms();
            signedCms.Decode(encodedSignature.AsSpan(4).ToArray());

            var signer = signedCms.SignerInfos.Count == 1
                ? signedCms.SignerInfos[0]
                : null;
            var certificate = signer?.Certificate;

            return certificate is null
                ? new(false, "SignatureInvalid")
                : new(
                    true,
                    "BundleSignatureVerified",
                    certificate.Subject,
                    certificate.Thumbprint,
                    trustResult);
        }
        catch (CryptographicException)
        {
            return new(false, "SignatureInvalid");
        }
        catch (InvalidDataException)
        {
            return new(false, "SignatureInvalid");
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"MSIX bundle signer read failed: {exception}");
            return new(false, "SignatureReadFailed");
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.WriteLine($"MSIX bundle signer access failed: {exception}");
            return new(false, "SignatureReadFailed");
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [In] Guid pgActionID, ref WinTrustData pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;

        public WinTrustFileInfo(string filePath)
        {
            cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>();
            pcwszFilePath = filePath;
            hFile = IntPtr.Zero;
            pgKnownSubject = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;

        public WinTrustData(IntPtr fileInfoPointer, uint stateAction)
        {
            cbStruct = (uint)Marshal.SizeOf<WinTrustData>();
            pPolicyCallbackData = IntPtr.Zero;
            pSIPClientData = IntPtr.Zero;
            dwUIChoice = WtdUiNone;
            fdwRevocationChecks = WtdRevokeNone;
            dwUnionChoice = WtdChoiceFile;
            pFile = fileInfoPointer;
            dwStateAction = stateAction;
            hWVTStateData = IntPtr.Zero;
            pwszURLReference = null;
            dwProvFlags = WtdRevocationCheckNone;
            dwUIContext = 0;
            pSignatureSettings = IntPtr.Zero;
        }
    }
}
