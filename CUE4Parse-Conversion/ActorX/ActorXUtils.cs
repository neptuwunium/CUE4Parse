using System;
using System.Runtime.CompilerServices;
using System.Text;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.ActorX
{
    public static class ActorXUtils
    {
        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static void SerializeChunkHeader(this FArchiveWriter Ar, VChunkHeader header, string name)
        {
            header.ChunkId = name;
            header.Serialize(Ar);
        }

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static void Write(this FArchiveWriter Ar, string value, int len)
        {
            var padded = new byte[len];
            var bytes = Encoding.UTF8.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
            Ar.Write(padded);
        }
    }
}