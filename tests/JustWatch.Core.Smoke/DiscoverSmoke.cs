using System.Net;
using System.Text;
using System.Text.Json;
using JustWatch.Plugin.Core;

static class DiscoverSmoke
{
    public static async Task Run()
    {
        var handler=new Handler();using var http=new HttpClient(handler);
        var client=new JustWatchClient(http,new JustWatchOptions{BaseUrl="https://discover-test.example.com",Country="US",SeerrUrl="https://seerr.example.com",SeerrApiKey="test-key"});
        var providers=await client.ProvidersAsync("ca",default);
        Check(providers.Count==2 && providers.Any(provider=>provider.Code=="nfx"),"regional catalog");
        var first=await client.DiscoverAsync("CA","movie","nfx",null,default);
        Check(first.Results.Count==1 && first.Results[0].Title=="Included" && first.Results[0].Country=="CA","unknown/off-provider offers not leaked into filtered browse");
        Check(first.Results[0].CanRequestInSeerr && first.Results[0].SeerrUrl=="https://seerr.example.com/movie/123","exact request identity retained");
        Check(first.HasNextPage && first.NextCursor=="MTI=","opaque pagination cursor");
        using(var request=JsonDocument.Parse(handler.LastRequest!))
        {
            var variables=request.RootElement.GetProperty("variables");
            Check(variables.GetProperty("filter").GetProperty("packages")[0].GetString()=="nfx","upstream provider filter");
            Check(variables.GetProperty("filter").GetProperty("monetizationTypes")[0].GetString()=="FLATRATE","subscription filter");
            Check(variables.GetProperty("filter").GetProperty("objectTypes")[0].GetString()=="MOVIE","movie filter");
        }
        var next=await client.DiscoverAsync("CA","movie","nfx","MTI=",default);
        Check(!next.HasNextPage,"repeated cursor terminates paging");
        Check(handler.CatalogCalls==1,"public service catalog is cached");
        var calls=handler.TitleCalls;
        try{await client.DiscoverAsync("CA",null,"unknown",null,default);throw new Exception("unknown provider accepted");}catch(ArgumentException){}
        Check(handler.TitleCalls==calls,"unknown provider must not silently fall through to unfiltered browse");
        try{await client.DiscoverAsync("CA",null,null,new string('a',513),default);throw new Exception("oversized cursor accepted");}catch(ArgumentException){}
        Console.WriteLine("Discover checks passed: regional providers, upstream filters, offer verification, identity, caching and pagination.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    sealed class Handler:HttpMessageHandler
    {
        public int CatalogCalls,TitleCalls;public string? LastRequest;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var text=await request.Content!.ReadAsStringAsync(token);
            using var document=JsonDocument.Parse(text);
            var query=document.RootElement.GetProperty("query").GetString()!;
            string body;
            if(query.Contains("packages(country")) {CatalogCalls++;body="""
{"data":{"packages":[{"packageId":8,"clearName":"Netflix","shortName":"nfx"},{"packageId":9,"clearName":"Other","shortName":"oth"}]}}
""";}
            else {TitleCalls++;LastRequest=text;body="""
{"data":{"popularTitles":{"edges":[{"node":{"id":"tm123","objectType":"MOVIE","content":{"title":"Included","externalIds":{"tmdbId":"123"}},"offers":[{"monetizationType":"FLATRATE","package":{"packageId":8,"clearName":"Netflix"}}]}},{"node":{"id":"tm456","objectType":"MOVIE","content":{"title":"Other service"},"offers":[{"monetizationType":"FLATRATE","package":{"packageId":9,"clearName":"Other"}}]}}],"pageInfo":{"endCursor":"MTI=","hasNextPage":true}}}}
""";}
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
}
