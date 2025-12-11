using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AssetDumper.Worlds;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Landscape;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Textures;
using CUE4Parse;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Internationalization;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Shaders;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using DragonLib;
using DragonLib.CommandLine;
using DragonLib.IO.DataReader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pepper;
using Serilog;
using Serilog.Events;

namespace AssetDumper;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum EComponentCreationMethod {
	/** A component that is part of a native class. */
	Native,

	/** A component that is created from a template defined in the Components section of the Blueprint. */
	SimpleConstructionScript,

	/**A dynamically created component, either from the UserConstructionScript or from a Add Component node in a Blueprint event graph. */
	UserConstructionScript,

	/** A component added to a single Actor instance via the Component section of the Actor's details panel. */
	Instance,
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public record UnrealVersionInfo {
	public int MajorVersion { get; set; }
	public int MinorVersion { get; set; }
	public int PatchVersion { get; set; }
	public int Changelist { get; set; }
	public int CompatibleChangelist { get; set; }
	public int IsLicenseeVersion { get; set; }
	public int IsPromotedBuild { get; set; }
	public string? BranchName { get; set; }
}

public static class Program {
	[SuppressMessage("ReSharper.DPA", "DPA0001: Memory allocation issues")]
	public static async Task Main() {
		var flags = CommandLineFlagsParser.ParseFlags<Flags>();

		if (flags.NoRevorb) {
			WemHelper.CanUseRevorb = false;
		}

		Globals.WarnMissingImportPackage = true;
		// Globals.AllowLargeFiles = true;

		OodleHelper.LoadOodleDll(Environment.CurrentDirectory);
		switch (Environment.OSVersion.Platform) {
			case PlatformID.MacOSX: ZlibHelper.Initialize("libz-ng.dylib"); break;
			case PlatformID.Unix: ZlibHelper.Initialize("libz-ng.so"); break;
			default:
				ZlibHelper.DownloadDll();
				ZlibHelper.Initialize("libz-ng.dll");
				break;
		}

		var target = Path.GetFullPath(flags.OutputPath);
		var targetBaseDir = new DirectoryInfo(Path.Combine(target, "Content"));
		targetBaseDir.Create();

		var logPath = Path.Combine(target, "Log.txt");
		if (File.Exists(logPath)) {
			File.Delete(logPath);
		}

		Log.Logger = new LoggerConfiguration()
					 .WriteTo.Console()
					 .WriteTo.File(logPath, LogEventLevel.Information)
					 .CreateLogger();

		if (flags.Game == EGame.GAME_AUTODETECT) {
			if (FindVersion(flags, out var version)) {
				Log.Information("Detected version as {Version}", version);
				flags.Game = version;
			} else {
				Log.Error("Could not autodetect version");
				return;
			}
		}

		if (!flags.Dry) {
			await using var assetDumperStream = new FileStream(Path.Combine(target, "CUE4AssetDumper.root"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
			await using var assetDumperWriter = new StreamWriter(assetDumperStream, Encoding.UTF8, leaveOpen: true);

			if (flags.LoadArgs) {
				using var assetDumperReader = new StreamReader(assetDumperStream, Encoding.UTF8, leaveOpen: true);
				var existing = JsonConvert.DeserializeObject<Flags>(await assetDumperReader.ReadToEndAsync(), new StringEnumConverter());
				if (existing is not null) {
					flags = existing;
				}

				flags.Dry = false;
			}

			assetDumperStream.SetLength(0);

			await assetDumperWriter.WriteAsync(JsonConvert.SerializeObject(flags, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.Default, Converters = { new StringEnumConverter() } }));
		}

		var versionOverrides = new Dictionary<string, bool>();
		foreach (var kv in flags.Versions.Select(version => version.Split('=', 2, StringSplitOptions.TrimEntries))) {
			versionOverrides[kv[0]] = bool.TryParse(kv.ElementAtOrDefault(1) ?? "true", out var value) && value;
			Log.Information("Setting {Key} to {Value}", kv[0], versionOverrides[kv[0]]);
		}

		var mapOverrides = new Dictionary<string, KeyValuePair<string, string>>();
		if (File.Exists(flags.MapStruct)) {
			Log.Information("Loading MapStruct File {File}", flags.MapStruct);
			mapOverrides = JsonConvert.DeserializeObject<Dictionary<string, KeyValuePair<string, string>>>(await File.ReadAllTextAsync(flags.MapStruct));
		}

		VersionContainer.DEFAULT_VERSION_CONTAINER.Game = flags.Game;
		VersionContainer.DEFAULT_VERSION_CONTAINER.Platform = flags.Platform;
		VersionContainer.DEFAULT_VERSION_CONTAINER.OptionOverrides = versionOverrides;
		VersionContainer.DEFAULT_VERSION_CONTAINER.MapStructTypesOverrides = mapOverrides;
		using var Provider = new DefaultFileProvider(Path.GetFullPath(flags.PakPath), SearchOption.AllDirectories, VersionContainer.DEFAULT_VERSION_CONTAINER, StringComparer.OrdinalIgnoreCase);
		Provider.UseLazyPackageSerialization = false;

		flags.Mappings ??= Directory.GetFiles(flags.PakPath, "*.usmap", SearchOption.AllDirectories).SingleOrDefault();
		if (File.Exists(flags.Mappings)) {
			Log.Information("Loading Mappings File {File}", flags.Mappings);
			Provider.MappingsContainer = new FileUsmapTypeMappingsProvider(flags.Mappings);
		}

		Provider.Initialize();
		if (flags.Keys.Count > 0) {
			var keys = flags.Keys.Select(x => new FAesKey(x)).ToList();
			var guids = flags.KeyGuids.Select(x => new FGuid(x)).ToList();

			if (guids.Count > 0) {
				await Provider.SubmitKeysAsync(guids.Zip(keys).Select(x => new KeyValuePair<FGuid, FAesKey>(x.First, x.Second)));
			}

			var remain = keys.Skip(guids.Count).ToArray();
			if (remain.Length > 0) {
				var foundKeys = new Dictionary<FGuid, FAesKey>();
				foreach (var reader in Provider.UnloadedVfs) {
					if (foundKeys.ContainsKey(reader.EncryptionKeyGuid) || !reader.IsEncrypted) {
						continue;
					}

					foreach (var key in keys) {
						var valid = reader.TestAesKeyCustom(key);

						if (!valid) {
							continue;
						}

						Log.Information("Validated Key {Guid}={Key}", reader.EncryptionKeyGuid, key);
						foundKeys[reader.EncryptionKeyGuid] = key;
					}
				}

				await Provider.SubmitKeysAsync(foundKeys);
			}
		}

		if (File.Exists(Path.Combine(target, "keys.txt"))) {
			var lines = await File.ReadAllLinesAsync(Path.Combine(target, "keys.txt"));
			var keys = lines.Where(x => x.Trim().Length >= 32)
							.Select(x => {
								// formats:
								// key{s;32}
								// guid{s:32} key{s:32}
								x = x.Trim();
								var hasGuid = x.Length > 32;
								var guid = new FGuid();
								if (hasGuid) {
									guid = new FGuid(x[..32].Trim());
								}

								return new KeyValuePair<FGuid, FAesKey>(guid, new FAesKey(x));
							})
							.Where(x => !string.IsNullOrEmpty(x.Value.KeyString));

			await Provider.SubmitKeysAsync(keys);
		}

		await Provider.MountAsync();

		Provider.LoadVirtualPaths();

		Globals.SkipObjectClasses.UnionWith(flags.SkipClasses);

		if (flags.SkipProblematicClasses) {
			Globals.SkipObjectClasses.Add("MapBuildDataRegistry");
			Globals.SkipObjectClasses.Add("MovieSceneCompiledData");
			Globals.SkipObjectClasses.Add("MovieSceneEventParameters");
			Globals.SkipObjectClasses.Add("MovieSceneEventSection");
			Globals.SkipObjectClasses.Add("MovieSceneSegment");
			Globals.SkipObjectClasses.Add("WidgetAnimation");
			Globals.SkipObjectClasses.Add("LevelSequence");
			Globals.SkipObjectClasses.Add("TimelineComponent");
			Globals.SkipObjectClasses.Add("NiagaraScript");
			Globals.SkipObjectClasses.Add("NiagaraMeshRendererProperties");
		}

		foreach (var keyGuid in Provider.RequiredKeys) {
			if (!Provider.Keys.ContainsKey(keyGuid)) {
				Log.Error("Requires missing encryption key 0x{Key}", keyGuid.ToString(EGuidFormats.Digits));
			}
		}

		var wwiseNames = new HashSet<string>();
		var wwiseRename = new Dictionary<string, string>();
		var wemList = new List<string>();
		var bnkList = new List<string>();
		flags.WwiseEventRoot ??= flags.WwiseMediaRoot;
		flags.WwiseMediaRoot ??= flags.WwiseEventRoot;

		var filesEnumerable = Provider.Files.Where(x => x.Value is VfsEntry).Where(x => x.Value.Extension is not ("ubulk" or "uexp" or "uptnl")).DistinctBy(x => x.Key);
		if (flags.Filters.Count > 0) {
			filesEnumerable = filesEnumerable.Where(x => flags.Filters.Any(y => y.IsMatch(x.Key)));
		}

		if (flags.Ignore.Count > 0) {
			filesEnumerable = filesEnumerable.Where(x => !flags.Ignore.All(y => y.IsMatch(x.Key)));
		}

		if (!string.IsNullOrEmpty(flags.Skip)) {
			filesEnumerable = filesEnumerable.SkipWhile(x => !x.Key.Contains(flags.Skip, StringComparison.OrdinalIgnoreCase));
		}

		var files = filesEnumerable.ToArray();
		var count = (float) files.Length;
		var processed = 0;

		var exportOptions = new ExporterOptions {
			MeshFormat = flags.MeshFormat, AnimFormat = flags.AnimationFormat, MaterialFormat = flags.MaterialFormat, TextureFormat = flags.TextureFormat, Platform = flags.Platform, SocketFormat = flags.SocketFormat, LodFormat = flags.LodFormat, ExportMaterials = false, ExportMaterialTextures = false, ExportMorphTargets = true,
		};

		foreach (var (path, gameFile) in files) {
			var pc = ++processed / count * 100;

			if (gameFile is VfsEntry vfs) {
				Log.Information("{Percent:N3}% {GameFile} from {Source}", pc, gameFile, vfs.Vfs.Name);
			} else {
				Log.Information("{Percent:N3}% {GameFile}", pc, gameFile);
			}

			var normalizedGamePath = gameFile.Path.TrimStart('/', '\\');

			if (flags.SaveRaw) {
				foreach (var subType in new[] { "uasset", "uexp", "uptnl", "ubulk" }) {
					if (!Provider.Files.TryGetValue(gameFile.PathWithoutExtension + "." + subType, out var subFile)) {
						continue;
					}

					var rawPath = Path.Combine(target, "Raw", Path.ChangeExtension(normalizedGamePath, subType));
					rawPath.EnsureDirectoryExists();
					var data = await subFile.ReadAsync();
					await File.WriteAllBytesAsync(rawPath, data);
				}
			}

			if (flags.Dry) {
				continue;
			}

			var targetGameFile = Path.Combine(target, "Content", normalizedGamePath);
			var targetJsonPath = Path.Combine(target, "Json", normalizedGamePath + ".json");

			try {
				var ext = gameFile.Extension.ToLowerInvariant();
				switch (ext) {
					default: {
						if (flags.NoUnknown) {
							break;
						}

						if (Provider.TrySaveAsset(path, out var data)) {
							targetGameFile.EnsureDirectoryExists();
							await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.ReadWrite);
							await stream.WriteAsync(data, CancellationToken.None);
						}

						break;
					}

					case "json":
					case "txt":
					case "xml":
					case "ini":
					case "uplugin":
					case "uproject":
					case "upluginmanifest": {
						var isSoundbankInfo = Path.GetFileName(normalizedGamePath).Equals("SoundbanksInfo.json", StringComparison.OrdinalIgnoreCase) ||
							Path.GetFileName(normalizedGamePath).Equals("SoundbanksInfo.xml", StringComparison.OrdinalIgnoreCase);

						if (normalizedGamePath.Contains("WWiseBankExport", StringComparison.OrdinalIgnoreCase)) {
							isSoundbankInfo = isSoundbankInfo || normalizedGamePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || normalizedGamePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
						}

						if (flags.NoConfig && !isSoundbankInfo) {
							break;
						}

						if (Provider.TrySaveAsset(path, out var data)) {
							if (!flags.NoConfig) {
								targetGameFile.EnsureDirectoryExists();
								await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.ReadWrite);
								await stream.WriteAsync(data, CancellationToken.None);
							}

							if (isSoundbankInfo) {
								using var reader = new StringReader(Encoding.UTF8.GetString(data));
								var info = new WwiseSoundbanksInfo(reader, normalizedGamePath);
								foreach (var stream in info.SoundBanksInfo.StreamedFiles) {
									wwiseRename[stream.Id + ".wem"] = stream.Language + "/" + stream.ShortName;
									wwiseRename[stream.Id + ".wem"] = stream.Language + "/" + stream.ShortName;
								}

								foreach (var stream in info.SoundBanksInfo.SoundBanks.SelectMany(bank => bank.IncludedMemoryFiles)) {
									wwiseRename[stream.Id + ".wem"] = stream.Language + "/" + stream.ShortName;
								}

								foreach (var stream in info.SoundBanksInfo.SoundBanks.SelectMany(bank => bank.IncludedEvents).SelectMany(x => x.IncludedMemoryFiles)) {
									wwiseRename[stream.Id + ".wem"] = stream.Language + "/" + stream.ShortName;
								}
							}
						}

						break;
					}

					case "bnk":
					case "wem": {
						if (flags.NoSounds) {
							break;
						}

						if (Provider.TrySaveAsset(path, out var data)) {
							targetGameFile.EnsureDirectoryExists();
							if (ext == "wem" && flags.ConvertWwiseSounds) {
								try {
									unsafe {
										fixed (byte* dataPin = &data[0]) {
											using var memoryStream = new UnmanagedMemoryStream(dataPin, data.Length);
											using var codec = WemHelper.GetDecoder(memoryStream);
											var newExt = codec.Format.ToString("G").ToLower();
											targetGameFile = Path.ChangeExtension(targetGameFile, newExt);
											normalizedGamePath = Path.ChangeExtension(normalizedGamePath, newExt);
											using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.ReadWrite);
											codec.Decode(stream);
										}
									}
								} catch (Exception e) {
									Log.Error(e, "Failed converting wem");
									File.Delete(targetGameFile);
									targetGameFile = Path.ChangeExtension(targetGameFile, "wem");
									normalizedGamePath = Path.ChangeExtension(normalizedGamePath, "wem");
									await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.ReadWrite);
									await stream.WriteAsync(data, CancellationToken.None);
								}
							} else {
								await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.ReadWrite);
								await stream.WriteAsync(data, CancellationToken.None);
							}

							if (ext == "wem") {
								wemList.Add(normalizedGamePath);
							} else {
								bnkList.Add(normalizedGamePath);
							}
						}

						break;
					}

					case "locres": {
						if (flags.NoJSON) {
							break;
						}

						if (Provider.TryCreateReader(path, out var reader)) {
							await using var archive = reader;
							targetJsonPath.EnsureDirectoryExists();
							await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationResource(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
						}

						break;
					}

					case "locmeta": {
						if (flags.NoJSON) {
							break;
						}

						if (Provider.TryCreateReader(path, out var reader)) {
							await using var archive = reader;
							targetJsonPath.EnsureDirectoryExists();
							await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationMetaDataResource(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
						}

						break;
					}

					case "ushaderbytecode":
					case "ushadercode": {
						if (flags.NoJSON) {
							break;
						}

						var package = await Provider.LoadPackageAsync(path);
						var name = package.Name.TrimStart('/', '\\');
						targetJsonPath = Path.Combine(target, "Json", name + ".json");
						if (Provider.TryCreateReader(path, out var reader)) {
							await using var archive = reader;
							targetJsonPath.EnsureDirectoryExists();
							await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FShaderCodeArchive(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
						}

						break;
					}

					case "uasset":
					case "umap" when !flags.SkipUMap: {
						try {
							var package = await Provider.LoadPackageAsync(path);
							var name = package.Name.TrimStart('/', '\\');
							targetJsonPath = Path.Combine(target, "Json", name + ".json");
							var exports = package.GetExports().ToArray();

							if (!flags.NoJSON) {
								// FMovieScene causes a lot of out-of-memory issues while serializing.
								targetJsonPath.EnsureDirectoryExists();
								try {
									await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(exports.Select(x => x.Class?.Name.StartsWith("MovieScene") == false ? x : default), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
								} catch (Exception e) {
									Log.Error(e, "Failed to convert UObject exports to JSON");
								}
							}

							for (var exportIndex = 0; exportIndex < exports.Length; exportIndex++) {
								var export = exports[exportIndex];
								var eventPath = ExporterBase.GetExportSavePath(export);
								var targetPath = Path.Combine(target, "Content", ExporterBase.GetExportSavePath(export));

								if (export is UAkAudioEvent akAudioEvent) {
									wwiseNames.Add(export.Name);

									var eventRoot = string.Empty;
									if (!string.IsNullOrEmpty(flags.WwiseEventRoot) && eventPath.StartsWith(flags.WwiseEventRoot)) {
										eventRoot = eventPath[flags.WwiseEventRoot.Length..];
									}

									if (flags.RenameWwiseAudio || flags.TrackWwiseEvents) {
										if (akAudioEvent.EventCookedData is { } eventData) {
											if (flags.TrackWwiseEvents) {
												wwiseNames.Add(eventData.DebugName.PlainText);
											}

											foreach (var (locale, _entry) in eventData.EventLanguageMap) {
												if (_entry is not { } entry) {
													continue;
												}

												if (flags.TrackWwiseEvents) {
													var allDebug = entry.Media.Select(x => x.DebugName.PlainText)
																		.Concat(entry.ExternalSources.Select(x => x.DebugName.PlainText))
																		.Concat(entry.SoundBanks.Select(x => x.DebugName.PlainText))
																		.Concat(entry.SwitchContainerLeaves.SelectMany(x => x.Media).Select(x => x.DebugName.PlainText))
																		.Concat(entry.SwitchContainerLeaves.SelectMany(x => x.ExternalSources).Select(x => x.DebugName.PlainText))
																		.Concat(entry.SwitchContainerLeaves.SelectMany(x => x.SoundBanks).Select(x => x.DebugName.PlainText));

													foreach (var media in allDebug) {
														wwiseNames.Add(media);
													}
												}

												if (!flags.RenameWwiseAudio) {
													continue;
												}

												var mediaEventRoot = eventRoot;
												if (!string.IsNullOrEmpty(mediaEventRoot)) {
													if (entry.Media.Length == 1) {
														mediaEventRoot = Path.GetDirectoryName(mediaEventRoot);
													}

													mediaEventRoot += "/";
												}

												foreach (var media in entry.Media) {
													wwiseRename[flags.WwiseMediaRoot + media.MediaPathName.PlainText] = locale.LanguageName.PlainText + "/" + mediaEventRoot + media.DebugName.PlainText.Replace('\\', '/').Replace(':', '_').Replace("..", "_", StringComparison.Ordinal);
												}
											}
										}
									}
								}

								try {
									switch (export) {
										case UTexture2D texture2D when !flags.NoTextures: {
											var texture = texture2D.Decode();
											if (texture != null) {
												targetPath.EnsureDirectoryExists();
												var data = texture.Encode(flags.TextureFormat, false, out var texExt);
												await using var fs = new FileStream(targetPath + $".{texExt}", FileMode.Create, FileAccess.ReadWrite);
												fs.Write(data);
											}

											break;
										}
										// case AnimatedTexture2D animated2d when !flags.NoTextures: {
										//     var texture = animated2d.FileBlob;
										//     if (texture.Length > 0 && animated2d.FileType != AnimatedTextureType.None) {
										//         targetPath.EnsureDirectoryExists();
										//         await using var fs = new FileStream(targetPath + $".{animated2d.FileType.ToString("G").ToLower()}", FileMode.Create, FileAccess.ReadWrite);
										//         await fs.WriteAsync(texture);
										//     }
										//
										//     break;
										// }
										case UAkMediaAssetData or USoundWave when !flags.NoSounds: {
											export.Decode(true, out var format, out var data);
											if (data != null && !string.IsNullOrEmpty(format)) {
												targetPath.EnsureDirectoryExists();
												await using var stream = new FileStream(targetPath + $".{format}", FileMode.Create, FileAccess.ReadWrite);
												await stream.WriteAsync(data, CancellationToken.None);
											}

											break;
										}
										case UAnimSequence animSequence when flags is { NoAnimations: false, NoAnimationSequences: false }: {
											targetPath.EnsureDirectoryExists();
											var exporter = new AnimExporter(animSequence, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UAnimMontage animMontage when flags is { NoAnimations: false, NoAnimationMontages: false }: {
											targetPath.EnsureDirectoryExists();
											var exporter = new AnimExporter(animMontage, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UAnimComposite animComposite when flags is { NoAnimations: false, NoAnimationComposites: false }: {
											targetPath.EnsureDirectoryExists();
											var exporter = new AnimExporter(animComposite, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UMaterialInterface material when !flags.NoMaterial: {
											targetPath.EnsureDirectoryExists();
											var exporter = new BasicMaterialExporter(material, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case USkeletalMesh skeletalMesh when !flags.NoMeshes: {
											targetPath.EnsureDirectoryExists();
											var exporter = new MeshExporter(skeletalMesh, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case USkeleton skeleton when !flags.NoMeshes: {
											targetPath.EnsureDirectoryExists();
											var exporter = new MeshExporter(skeleton, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UStaticMesh staticMesh when !flags.NoMeshes: {
											targetPath.EnsureDirectoryExists();
											var exporter = new MeshExporter(staticMesh, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UWorld world when !flags.NoWorlds: {
											targetPath.EnsureDirectoryExists();
											var exporter = new WorldExporter(world, flags.Platform, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case ALandscape landscape when !flags.NoWorlds: {
											targetPath.EnsureDirectoryExists();
											var exporter = new LandscapeExporter(landscape, null, exportOptions);
											exporter.TryWriteToDir(targetBaseDir, out _, out _);
											break;
										}
										case UDataTable dataTable when !flags.NoDataTable: {
											targetPath.EnsureDirectoryExists();
											await File.WriteAllTextAsync($"{targetPath}.json", JsonConvert.SerializeObject(dataTable, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
											break;
										}
										case UStringTable stringTable when !flags.NoStringTable: {
											targetPath.EnsureDirectoryExists();
											await File.WriteAllTextAsync($"{targetPath}.json", JsonConvert.SerializeObject(stringTable, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
											break;
										}
										default: {
											if (!flags.NoBlueprints) {
												// note: despite the name we're actually exporting SCS objects, not blueprints specifically.
												var creationMethod = export.GetOrDefault<EComponentCreationMethod?>("CreationMethod");
												// if not marked as a SCS actor and is not a blueprint class:
												if (creationMethod is not EComponentCreationMethod.SimpleConstructionScript && export is not UBlueprintGeneratedClass) {
													break;
												}

												var targetBpPath = Path.Combine(target, "Blueprint", normalizedGamePath + ".json");
												targetBpPath.EnsureDirectoryExists();
												var merged = BlueprintConstructor.GetMergedStruct(export);
												await File.WriteAllTextAsync(targetBpPath, JsonConvert.SerializeObject(merged, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
											}

											break;
										}
									}
								} catch (Exception e) {
									if (Debugger.IsAttached) {
										throw;
									}

									Log.Error(e, "Failed to convert UObject export #{Export}", exportIndex);
								}
							}
						} catch (Exception e) {
							Log.Error(e, "Failed to process file");
						}

						break;
					}
				}
			} catch (Exception e) {
				Log.Error(e, "Unknown error");
			}
		}

		if (flags.ExtractWwiseMemory) {
			var wwisePath = Path.Combine(target, "Wwise");
			foreach (var bnkPath in bnkList) {
				try {
					var fallbackPath = Path.ChangeExtension(bnkPath.Replace('\\', '/'), null);
					var gamePath = Path.Combine(target, "Content", bnkPath);
					await using var bnkStream = File.OpenRead(gamePath);
					using var bnk = new WwiseSoundbank(bnkStream);

					foreach (var audioId in bnk.DataIndex.Keys) {
						var mediaName = $"{audioId:D}.wem";

						if (flags.RenameWwiseAudio && wwiseRename.TryGetValue(mediaName, out var realPath)) {
							mediaName = Path.Combine(wwisePath, realPath.Replace('\\', '/'));
							Log.Information("Extracting Wwise {AudioId:D} -> {RealPath}", audioId, realPath);
						} else {
							mediaName = Path.Combine(wwisePath, fallbackPath, mediaName);
							Log.Information("Extracting Wwise {AudioId:D}", audioId);
						}

						mediaName.EnsureDirectoryExists();

						using var rented = bnk.RentSound(audioId, out var size);

						if (flags.ConvertWwiseSounds) {
							try {
								unsafe {
									using var pinned = rented.Memory.Pin();
									using var memoryStream = new UnmanagedMemoryStream((byte*) pinned.Pointer, Math.Min(size, rented.Memory.Length));
									using var codec = WemHelper.GetDecoder(memoryStream);
									var newExt = codec.Format.ToString("G").ToLower();
									using var stream = new FileStream(Path.ChangeExtension(mediaName, newExt), FileMode.Create, FileAccess.ReadWrite);
									codec.Decode(stream);
								}
							} catch(Exception e) {
								Log.Error(e, "Failed converting wem");
								await using var output = new FileStream(Path.ChangeExtension(mediaName, "wem"), FileMode.Create, FileAccess.ReadWrite);
								output.Write(rented.Memory.Span[..size]);
							}
						} else {
							await using var output = new FileStream(Path.ChangeExtension(mediaName, "wem"), FileMode.Create, FileAccess.ReadWrite);
							output.Write(rented.Memory.Span[..size]);
						}
					}
				} catch (Exception e) {
					Log.Error(e, "Failed extracting audio from bnk");
				}
			}
		}

		if (flags.RenameWwiseAudio) {
			foreach (var (hashedPath, realPath) in wwiseRename) {
				try {
					if (string.IsNullOrEmpty(realPath)) {
						continue;
					}

					var gamePath = wemList.FirstOrDefault(x => Path.ChangeExtension(x, null).EndsWith(Path.ChangeExtension(hashedPath, null), StringComparison.OrdinalIgnoreCase));
					if (gamePath is null) {
						continue;
					}

					Log.Information("Renaming Wwise {HashedPath} -> {RealPath}", hashedPath, realPath);

					var path = Path.Combine(target, "Wwise", realPath.Replace('\\', '/'));
					path.EnsureDirectoryExists();
					path = Path.ChangeExtension(path, Path.GetExtension(gamePath));
					File.Move(Path.Combine(target, "Content", gamePath), path, true);
				} catch (Exception e) {
					Log.Error(e, "Failed renaming audio");
				}
			}
		}

		if (flags.TrackWwiseEvents) {
			if (File.Exists(Path.Combine(target, "wwnames.txt"))) {
				wwiseNames.UnionWith(await File.ReadAllLinesAsync(Path.Combine(target, "wwnames.txt")));
			}

			wwiseNames.UnionWith(wwiseRename.Values);

			await File.WriteAllTextAsync(Path.Combine(target, "wwnames.txt"), string.Join('\n', wwiseNames));
		}
	}

	private static bool TryParseUnrealVersion(string versionString, out EGame version) {
		version = EGame.GAME_AUTODETECT;
		var str = versionString.Split('-');
		Log.Information("Found version candidate {Name}", str);
		if (str.Length == 1) {
			return false;
		}

		if (str[1].StartsWith("4.25Plus")) {
			version = EGame.GAME_UE4_25_Plus;
			return true;
		}

		foreach (var part in str) {
			var parts = part.Split('.');
			if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) {
				continue;
			}

			if (major < 4) {
				continue;
			}

			version = (EGame) (((major - 3) << 24) + minor << 4);

			return true;
		}

		return false;
	}

	private static bool FindVersion(Flags flags, out EGame version) {
		var utf8signature = Signature.CreateSignature("00 2B 2B 55 45");
		var utf16signature = Signature.CreateSignature("00 2B 00 2B 00 55 00 45 00");
		version = EGame.GAME_AUTODETECT;
		foreach (var path in Directory.GetFiles(flags.PakPath, "*", SearchOption.AllDirectories)) {
			if (new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory)) {
				continue;
			}

			var ext = Path.GetExtension(path).ToLower();
			if (!string.IsNullOrEmpty(ext)) {
				continue;
			}

			switch (ext) {
				case ".exe": {
					var executableData = File.ReadAllBytes(path).AsSpan();
					foreach (var hit in Signature.FindSignaturesReverse(executableData, utf8signature)) {
						var str = executableData[(hit + 1)..].ReadString(Encoding.UTF8, 64) ?? string.Empty;

						if (TryParseUnrealVersion(str, out version)) {
							return true;
						}
					}

					foreach (var hit in Signature.FindSignaturesReverse(executableData, utf16signature)) {
						var str = executableData[(hit + 1)..].ReadString(Encoding.Unicode, 64) ?? string.Empty;

						if (TryParseUnrealVersion(str, out version)) {
							return true;
						}
					}

					break;
				}
				case ".version": {
					try {
						var versionInfo = JsonConvert.DeserializeObject<UnrealVersionInfo>(File.ReadAllText(path));
						if (versionInfo == null) {
							continue;
						}

						if (!string.IsNullOrEmpty(versionInfo.BranchName) && TryParseUnrealVersion(versionInfo.BranchName, out version)) {
							return true;
						}

						var simulatedBranchName = $"++UE{versionInfo.MajorVersion}+Release-{versionInfo.MajorVersion}.{versionInfo.MinorVersion}_{versionInfo.CompatibleChangelist}";

						if (TryParseUnrealVersion(simulatedBranchName, out version)) {
							return true;
						}
					} catch {
						// ignored
					}

					break;
				}
			}
		}

		return false;
	}
}
