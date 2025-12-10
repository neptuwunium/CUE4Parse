using CUE4Parse.UE4.Objects.Core.Misc;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Objects.LevelSequence;

public readonly struct FLevelSequenceObjectReferenceMap : IUStruct, IReadOnlyDictionary<FGuid, FLevelSequenceLegacyObjectReference>
{
    public readonly IDictionary<FGuid, FLevelSequenceLegacyObjectReference> Map;

    public FLevelSequenceObjectReferenceMap(FArchive Ar)
    {
        Map = Ar.ReadMap(Ar.Read<FGuid>, () => new FLevelSequenceLegacyObjectReference(Ar));
    }

    public FLevelSequenceLegacyObjectReference this[FGuid key] => Map[key];
    public IEnumerable<FGuid> Keys => Map.Keys.AsEnumerable();
    public IEnumerable<FLevelSequenceLegacyObjectReference> Values => Map.Values.AsEnumerable();
    public int Count => Map.Count;

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public bool ContainsKey(FGuid key) => Map.ContainsKey(key);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public IEnumerator<KeyValuePair<FGuid, FLevelSequenceLegacyObjectReference>> GetEnumerator() => Map.GetEnumerator();

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    public bool TryGetValue(FGuid key, out FLevelSequenceLegacyObjectReference value) => Map.TryGetValue(key, out value);

    [MethodImpl(CUE4Parse.Globals.MethodOptions)]
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Map).GetEnumerator();
}
