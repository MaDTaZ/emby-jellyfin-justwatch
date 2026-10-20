using System.IO.Compression;
using System.Reflection;
using Jellyfin.Plugin.JustWatch;
using Jellyfin.Plugin.JustWatch.Api;
using JustWatch.Plugin.Core;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

var root=Path.Combine(Path.GetTempPath(),"wtw-maintenance-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var paths=Fake<IApplicationPaths>(method=>method.ReturnType==typeof(string)?root:null);
    var xml=Fake<IXmlSerializer>(_=>null);
    var current=new Plugin(paths,xml);
    var activeVersion=current.GetType().Assembly.GetName().Version!;
    var plugins=new List<LocalPlugin>();
    LocalPlugin Add(string name,Version version,Guid id,PluginStatus status,bool loaded=false)
    {
        var path=Path.Combine(root,"plugins",name+"_"+version);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path,"plugin.dll"),"test binary");
        var local=new LocalPlugin(path,true,new PluginManifest {Id=id,Name=name,Version=version.ToString(),Status=status});
        if(loaded) local.Instance=current;
        plugins.Add(local);
        return local;
    }
    var active=Add("Where to Watch",activeVersion,current.Id,PluginStatus.Active,true);
    var old=Add("JustWatch",new Version(0,1,6,0),current.Id,PluginStatus.Superseded);
    var other=Add("Unrelated",new Version(0,1,0,0),Guid.NewGuid(),PluginStatus.Disabled);
    var settings=Path.Combine(root,"Jellyfin.Plugin.JustWatch.xml");
    File.WriteAllText(settings,"saved config");
    var removed=new List<LocalPlugin>();
    var manager=DispatchProxy.Create<IPluginManager,Stub>();
    ((Stub)(object)manager).Call=(method,args)=>
    {
        if(method.Name=="get_Plugins")return plugins.ToArray();
        if(method.Name=="RemovePlugin")
        {
            var plugin=(LocalPlugin)args![0]!;
            if(!Directory.EnumerateFiles(Path.Combine(root,"where-to-watch-backups"),"*.zip").Any())throw new Exception("Removal before backup");
            removed.Add(plugin);Directory.Delete(plugin.Path,true);plugins.Remove(plugin);return true;
        }
        return null;
    };
    var result=new JustWatchController().Cleanup(manager);
    if(result.Value?.Success!=true || removed.Count!=1 || removed[0]!=old)throw new Exception("Only retired matching copy should be removed");
    if(!Directory.Exists(active.Path)||!Directory.Exists(other.Path)||File.ReadAllText(settings)!="saved config")throw new Exception("Active/unrelated/config changed");
    var archive=Directory.GetFiles(Path.Combine(root,"where-to-watch-backups"),"*.zip").Single();
    using(var zip=ZipFile.OpenRead(archive))
        if(zip.GetEntry("plugin.dll")==null)throw new Exception("Missing backup contents");
    if(new JustWatchController().Cleanup(manager).Value?.Success!=true || removed.Count!=1)throw new Exception("Cleanup must be repeatable");
    Directory.Move(Path.Combine(root,"where-to-watch-backups"),Path.Combine(root,"saved-backups"));
    File.WriteAllText(Path.Combine(root,"where-to-watch-backups"),"block backup creation");
    var blocked=Add("JustWatch",new Version(0,1,7,0),current.Id,PluginStatus.Disabled);
    var rejected=new JustWatchController().Cleanup(manager);
    if(rejected.Result is not Microsoft.AspNetCore.Mvc.BadRequestObjectResult || removed.Count!=1 || !Directory.Exists(blocked.Path))throw new Exception("Backup failure must prevent removal");
    foreach(var method in new[]{"Cleanup","TestSeerr"})
        if(!typeof(JustWatchController).GetMethod(method)!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any(attribute=>attribute.Policy==MediaBrowser.Common.Api.Policies.RequiresElevation))
            throw new Exception("Administrator policy missing: "+method);
    Console.WriteLine("Cleanup filesystem checks passed: backup-before-remove, backup failure prevents removal, current/unrelated/config retained, repeatable, administrator policies present.");
}
finally {Directory.Delete(root,true);}

static T Fake<T>(Func<MethodInfo,object?> call) where T:class
{
    var value=DispatchProxy.Create<T,Stub>();
    ((Stub)(object)value).Call=(method,_)=>call(method);
    return value;
}
public class Stub:DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Call=(_,_)=>null;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Call(method!,args);
}
