using System.IO;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.AssetRegistry.Readers;

public abstract class FAssetRegistryArchive : FArchive
{
    protected readonly FArchive baseArchive;
    public FAssetRegistryHeader Header;
    public FNameEntrySerialized[] NameMap;
    public bool IsFilterEditorOnly { get; set; }

    public abstract void SkipFName();
    public abstract void SkipTagsAndBundles();
    public abstract void SerializeTagsAndBundles(FAssetData assetData);    

    public FAssetRegistryArchive(FArchive Ar, FAssetRegistryHeader header) : base(Ar.Versions)
    {
        baseArchive = Ar;
        Header = header;
        NameMap = [];
    }

    public override string ReadFString()
    {
        return Header.Version >= FAssetRegistryVersionType.MarshalledTextAsUTF8String ? baseArchive.ReadFUtf8String() : baseArchive.ReadFString();
    }

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override int Read(byte[] buffer, int offset, int count)
        => baseArchive.Read(buffer, offset, count);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override long Seek(long offset, SeekOrigin origin)
        => baseArchive.Seek(offset, origin);

    public override bool CanSeek => baseArchive.CanSeek;
    public override long Length => baseArchive.Length;
    public override long Position
    {
        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        get => baseArchive.Position;
        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        set => baseArchive.Position = value;
    }

    public override string Name => baseArchive.Name;

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override T Read<T>()
        => baseArchive.Read<T>();

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override byte[] ReadBytes(int length)
        => baseArchive.ReadBytes(length);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override unsafe void Serialize(byte* ptr, int length)
        => baseArchive.Serialize(ptr, length);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override T[] ReadArray<T>(int length)
        => baseArchive.ReadArray<T>(length);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public override void ReadArray<T>(T[] array)
        => baseArchive.ReadArray(array);
}
