using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using DragonLib.CommandLine;

namespace AssetDumper;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public record Flags : CommandLineFlags {
    [Flag("raw", Help = "Dump raw uasset files")]
    public bool SaveRaw { get; set; }

    [Flag("wwise-events", Help = "Keep track of Wwise Events")]
    public bool TrackWwiseEvents { get; set; }

    [Flag("wwise", Help = "Rename Wwise WEMs if a DebugName is present")]
    public bool RenameWwiseAudio { get; set; }

    [Flag("wwise-bnk", Help = "Extract Wwise WEMs from BNKs")]
    public bool ExtractWwiseMemory { get; set; }

    [Flag("wwise-no-revorb", Help = "Do not Revorb Wwise WEMs")]
    public bool NoRevorb { get; set; }

    [Flag("no-problematic", Help = "Suppress Problematic classes")]
    public bool SkipProblematicClasses { get; set; }

    [Flag("no-json", Help = "Suppress JSON generation")]
    public bool NoJSON { get; set; }

    [Flag("no-datatable", Help = "Suppress DataTable conversion")]
    public bool NoDataTable { get; set; }

    [Flag("no-stringtable", Help = "Suppress StringTable conversion")]
    public bool NoStringTable { get; set; }

    [Flag("no-materials", Help = "Suppress Material conversion")]
    public bool NoMaterial { get; set; }

    [Flag("no-textures", Help = "Suppress Texture conversion")]
    public bool NoTextures { get; set; }

    [Flag("no-sounds", Help = "Suppress Sound conversion")]
    public bool NoSounds { get; set; }

    [Flag("convert-wwise", Help = "Convert Wwise to usable formats")]
    public bool ConvertWwiseSounds { get; set; }

    [Flag("no-meshes", Help = "Suppress Mesh conversion")]
    public bool NoMeshes { get; set; }

    [Flag("no-world", Help = "Suppress World conversion")]
    public bool NoWorlds { get; set; }

    [Flag("no-animations", Help = "Suppress Animation conversion")]
    public bool NoAnimations { get; set; }

    [Flag("no-animation-sequences", Help = "Suppress Animation Sequence conversion")]
    public bool NoAnimationSequences { get; set; }

    [Flag("no-animation-montages", Help = "Suppress Animation Montage conversion")]
    public bool NoAnimationMontages { get; set; }

    [Flag("no-animation-composites", Help = "Suppress Animation Composite conversion")]
    public bool NoAnimationComposites { get; set; }

    [Flag("no-config", Help = "Suppress Config files from being saved")]
    public bool NoConfig { get; set; }

    [Flag("no-unknown", Help = "Suppress unknown files from being saved")]
    public bool NoUnknown { get; set; }

    [Flag("skip-umap", Help = "Skip umaps")]
    public bool SkipUMap { get; set; }

    [Flag("dry", Help = "Only list files")]
    public bool Dry { get; set; }

    [Flag("usmap", Help = "Unreal Engine Struct Mappings")]
    public string? Mappings { get; set; }

    [Flag("aes", Aliases = ["k", "key", "keys"], Help = "AES key values for the packages")]
    public List<string> Keys { get; set; } = [];

    [Flag("guid", Aliases = ["K"], Help = "AES key guids for the packages")]
    public List<string> KeyGuids { get; set; } = [];

    [Flag("game", Help = "Unreal Version to use", EnumPrefix = ["GAME_"], ReplaceDashes = '_', ReplaceDots = '_')]
    public EGame Game { get; set; } = EGame.GAME_AUTODETECT;

    [Flag("lod", Help = "LOD export format")]
    public ELodFormat LodFormat { get; set; } = ELodFormat.FirstLod;

    [Flag("mesh-format", Help = "Mesh format to export to")]
    public EMeshFormat MeshFormat { get; set; } = EMeshFormat.UEFormat;

    [Flag("texture-format", Help = "Texture format to export to")]
    public ETextureFormat TextureFormat { get; set; } = ETextureFormat.Png;

    [Flag("anim-format", Help = "Animation format to export to")]
    public EAnimFormat AnimationFormat { get; set; } = EAnimFormat.UEFormat;

    [Flag("socket-format", Help = "Socket format to use")]
    public ESocketFormat SocketFormat { get; set; } = ESocketFormat.Socket;

    [Flag("material-format", Help = "Material format to use")]
    public EMaterialFormat MaterialFormat { get; set; } = EMaterialFormat.AllLayers;

    [Flag("platform", Help = "Platform of the game")]
    public ETexturePlatform Platform { get; set; } = ETexturePlatform.DesktopMobile;

    [Flag("filter", Help = "Path filters")]
    public List<Regex> Filters { get; set; } = [];

    [Flag("ignore", Help = "Path filters to ignore")]
    public List<Regex> Ignore { get; set; } = [];

    [Flag("skip-class", Aliases = ["e"], Help = "Classes to skip")]
    public HashSet<string> SkipClasses { get; set; } = [];

    [Flag("versions", Aliases = ["V"], Help = "Version Overrides")]
    public HashSet<string> Versions { get; set; } = [];

    [Flag("output-path", IsRequired = true, Positional = 1, Help = "Path to where to save files")]
    public string OutputPath { get; set; } = null!;

    [Flag("pak-path", IsRequired = true, Positional = 0, Help = "Path to where the packages are")]
    public string PakPath { get; set; } = null!;

    [Flag("load-args", Help = "Load Program Arguments from Root File")]
    public bool LoadArgs { get; set; }

    [Flag("mapstruct", Help = "path to the Map Struct override json")]
    public string? MapStruct { get; set; }

    [Flag("no-blueprints", Aliases = ["no-bp"], Help = "Do not output merged blueprints")]
    public bool NoBlueprints { get; set; }

    [Flag("skip", Help = "Skip until this path is found")]
    public string? Skip { get; set; }
}
