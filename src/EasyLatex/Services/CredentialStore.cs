using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;

namespace EasyLatex.Services;

public static class CredentialStore
{
    private static string Target(string endpoint) => "EasyLatex/API/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.Trim().TrimEnd('/'))))[..24];
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr pointer);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);
    public static string Read(string endpoint)
    {
        if (!CredRead(Target(endpoint), 1, 0, out var pointer)) return "";
        try { var c = Marshal.PtrToStructure<Credential>(pointer); return Marshal.PtrToStringUni(c.CredentialBlob, (int)c.CredentialBlobSize / 2) ?? ""; }
        finally { CredFree(pointer); }
    }
    public static void Write(string endpoint, string secret)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        if (bytes.Length > 2560) throw new ArgumentException("API 密钥过长。");
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = Target(endpoint), CredentialBlobSize = (uint)bytes.Length, CredentialBlob = blob, Persist = 2, UserName = "EasyLatex" };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法保存密钥到 Windows 凭据管理器。");
        }
        finally { for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0); Marshal.FreeHGlobal(blob); Array.Clear(bytes); }
    }
    public static void Delete(string endpoint) => CredDelete(Target(endpoint), 1, 0);
}
