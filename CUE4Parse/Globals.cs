using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CUE4Parse
{
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public static class Globals {
        public static HashSet<string> SkipObjectClasses = new();
        public static bool LogVfsMounts = true;
        public static bool FatalObjectSerializationErrors = false;
        public static bool WarnMissingImportPackage = true;
    }
}