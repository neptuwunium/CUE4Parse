using System.Runtime.CompilerServices;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.VirtualFileSystem;

public abstract partial class AbstractAesVfsReader : AbstractVfsReader, IAesVfsReader
{
    public abstract long Length { get; set; }
    public IAesVfsReader.CustomEncryptionDelegate? CustomEncryption { get; set; }
    public FAesKey? AesKey { get; set; }
    public CompressionMethod[] CompressionMethods { get; set; }

    public abstract FGuid EncryptionKeyGuid { get; }
    public abstract bool IsEncrypted { get; }

    public int EncryptedFileCount { get; protected set; }
    public bool bDecrypted { get; protected set; }

    protected AbstractAesVfsReader(string path, VersionContainer versions) : base(path, versions)
    {
        // yes
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public bool TestAesKey(FAesKey key) => !IsEncrypted || TestAesKey(MountPointCheckBytes(), key);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public bool TestAesKeyCustom(FAesKey key) => !IsEncrypted || TestAesKeyCustom(MountPointCheckBytes(), key);

    public abstract byte[] MountPointCheckBytes();

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public static bool TestAesKey(byte[] bytes, FAesKey key)
    {
        return IsValidIndex(bytes.Decrypt(key));
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public bool TestAesKeyCustom(byte[] bytes, FAesKey key) {
        return IsValidIndex(CustomEncryption != null ? CustomEncryption(bytes, 0, bytes.Length, true, this) : bytes.Decrypt(key));
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected byte[] Decrypt(byte[] bytes, FAesKey? key, bool bypassMountPointCheck = false)
    {
        if (bDecrypted)
        {
            return bytes.Decrypt(key!);
        }

        if (key != null && (TestAesKey(key) || bypassMountPointCheck))
        {
            bDecrypted = true;
            return bytes.Decrypt(key!);
        }
        throw new InvalidAesKeyException("Reading encrypted data requires a valid aes key");
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected byte[] DecryptIfEncrypted(byte[] bytes) => DecryptIfEncrypted(bytes, IsEncrypted);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected byte[] DecryptIfEncrypted(byte[] bytes, int beginOffset, int count) =>
        DecryptIfEncrypted(bytes, beginOffset, count, IsEncrypted);

    protected byte[] DecryptIfEncrypted(byte[] bytes, bool isEncrypted, bool isIndex = false)
    {
        if (!isEncrypted) return bytes;
        if (CustomEncryption != null)
        {
            return CustomEncryption(bytes, 0, bytes.Length, isIndex, this);
        }

        return Decrypt(bytes, AesKey);
    }

    protected byte[] DecryptIfEncrypted(byte[] bytes, int beginOffset, int count, bool isEncrypted, bool bypassMountPointCheck = false, bool isIndex = false)
    {
        if (!isEncrypted) return bytes;
        if (CustomEncryption != null)
        {
            return CustomEncryption(bytes, beginOffset, count, isIndex, this);
        }

        return Decrypt(bytes, AesKey, bypassMountPointCheck);
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected abstract byte[] ReadAndDecrypt(int length);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected virtual byte[] ReadAndDecryptIndex(int length) => ReadAndDecrypt(length);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected byte[] ReadAndDecrypt(int length, FArchive reader, bool isEncrypted) =>
        DecryptIfEncrypted(reader.ReadBytes(length), isEncrypted);

    protected byte[] ReadAndDecryptAt(long position, int length, FArchive reader, bool isEncrypted) =>
        DecryptIfEncrypted(reader.ReadBytesAt(position, length), isEncrypted);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    protected byte[] ReadAndDecryptIndex(int length, FArchive reader, bool isEncrypted) =>
        DecryptIfEncrypted(reader.ReadBytes(length), isEncrypted, true);

    protected byte[] ReadAndDecryptIndexAt(long position, int length, FArchive reader, bool isEncrypted) =>
        DecryptIfEncrypted(reader.ReadBytesAt(position, length), isEncrypted, true);
}
