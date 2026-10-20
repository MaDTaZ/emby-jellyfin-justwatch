using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

foreach(var file in new[]{"dist/JustWatch-Emby-Stable-4.8.10.zip","dist/JustWatch-Emby-Beta-4.11.0.8.zip"})
{
    using var archive=ZipFile.OpenRead(file);
    if(archive.Entries.Count!=1 || archive.Entries[0].FullName!="Emby.Plugin.JustWatch.dll")throw new Exception("Emby package must contain exactly one plugin DLL: "+file);
    using var bytes=new MemoryStream();
    using(var entry=archive.Entries[0].Open())entry.CopyTo(bytes);
    bytes.Position=0;
    using var pe=new PEReader(bytes);
    var metadata=pe.GetMetadataReader();
    foreach(var reference in metadata.AssemblyReferences)
        if(metadata.GetString(metadata.GetAssemblyReference(reference).Name)=="JustWatch.Plugin.Core")throw new Exception("External Core reference remains");
    var types=metadata.TypeDefinitions.Select(handle=>metadata.GetTypeDefinition(handle)).Select(type=>metadata.GetString(type.Namespace)+"."+metadata.GetString(type.Name)).ToHashSet();
    foreach(var name in new[]{"JustWatchClient","SeerrClient","JustWatchSearchResponse","SeerrRequest","SeerrResult"})
        if(!types.Contains("JustWatch.Plugin.Core."+name))throw new Exception("Missing compiled-in shared type: "+name);
    var resources=metadata.ManifestResources.Select(handle=>metadata.GetString(metadata.GetManifestResource(handle).Name)).ToHashSet();
    foreach(var suffix in new[]{"configPage.html","configPage.js"})
        if(!resources.Contains("Emby.Plugin.JustWatch.Configuration."+suffix))throw new Exception("Missing dashboard resource");
    Console.WriteLine(Path.GetFileName(file)+": single DLL, embedded shared types, no external Core reference, dashboard resources present.");
}
