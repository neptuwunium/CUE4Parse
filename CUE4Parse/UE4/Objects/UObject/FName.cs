using System;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.IO.Objects;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Objects.UObject
{

    [JsonConverter(typeof(FNameConverter))]
    public readonly struct FName : IComparable<FName>
    {
        private readonly FNameEntrySerialized _name;
        /** Index into the Names array (used to find String portion of the string/number pair used for display) */
        public readonly int Index;
        /** Number portion of the string/number pair (stored internally as 1 more than actual, so zero'd memory will be the default, no-instance case) */
        public readonly int Number;

        public readonly ulong Hash;

        public string Text => Number == 0 ? PlainText : $"{PlainText}_{Number - 1}";
        public string PlainText
        {
            [MethodImpl(CUE4Parse.Globals.MethodOptions)]
            get => _name.Name ?? "None";
        }
        public bool IsNone => Text == "None";


        public FName(string? name, int index = 0, int number = 0)
        {
            _name = new FNameEntrySerialized(name);
            Index = index;
            Number = number;
            Hash = _name.Hash;
        }

        public FName(FNameEntrySerialized name, int index, int number)
        {
            _name = name;
            Index = index;
            Number = number;
            Hash = name.Hash;
        }

        public FName(FNameEntrySerialized[] nameMap, int index, int number) : this(nameMap[index], index, number) { }

        public FName(FMappedName mappedName, FNameEntrySerialized[] nameMap) : this(nameMap, (int) mappedName.NameIndex, (int) mappedName.ExtraIndex) { }

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static implicit operator FName(string s) => new(s);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator ==(FName a, FName b) => a.Hash == b.Hash && a.Number == b.Number;

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator !=(FName a, FName b) => !(a == b);

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator ==(FName a, int b) => a.Index == b;

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator !=(FName a, int b) => a.Index != b;

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator ==(FName a, uint b) => a.Index == b;

        [MethodImpl(CUE4Parse.Globals.MethodOptions)]
        public static bool operator !=(FName a, uint b) => a.Index != b;

        public override bool Equals(object? obj) => obj is FName other && this == other;

        public override int GetHashCode() => HashCode.Combine(Hash, Number);
        
        public int CompareTo(FName other) => string.Compare(Text, other.Text, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => Text;
    }
}
