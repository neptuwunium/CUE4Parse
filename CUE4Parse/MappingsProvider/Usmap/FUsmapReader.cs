using System.IO;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.MappingsProvider.Usmap
{
    public class FUsmapReader : FArchive
    {
        protected readonly FArchive InnerArchive;
        public EUsmapVersion Version;

        public FUsmapReader(FArchive Ar, EUsmapVersion version) : base(Ar.Versions)
        {
            InnerArchive = Ar;
            Version = version;
        }

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override int Read(byte[] buffer, int offset, int count)
            => InnerArchive.Read(buffer, offset, count);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override long Seek(long offset, SeekOrigin origin)
            => InnerArchive.Seek(offset, origin);

        public override bool CanSeek => InnerArchive.CanSeek;
        public override long Length => InnerArchive.Length;

        public override long Position
        {
            [MethodImpl(CUE4Parse.Globals.MethodOptions)]
            get => InnerArchive.Position;
            [MethodImpl(CUE4Parse.Globals.MethodOptions)]
            set => InnerArchive.Position = value;
        }

        public override string Name => InnerArchive.Name;

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override T Read<T>()
            => InnerArchive.Read<T>();

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override byte[] ReadBytes(int length)
            => InnerArchive.ReadBytes(length);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override unsafe void Serialize(byte* ptr, int length)
            => InnerArchive.Serialize(ptr, length);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override T[] ReadArray<T>(int length)
            => InnerArchive.ReadArray<T>(length);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public override void ReadArray<T>(T[] array)
            => InnerArchive.ReadArray(array);

        public override object Clone() => new FUsmapReader((FArchive) InnerArchive.Clone(), Version);
    }
}