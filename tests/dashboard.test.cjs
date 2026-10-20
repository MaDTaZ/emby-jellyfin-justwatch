const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

async function render(host, results, fail = false, details = {State: "notRequested", CanRequest: true, Seasons: [{Number:1,Name:"Season 1"},{Number:2,Name:"Season 2"}]}) {
    const rows = Array.isArray(results) ? results : results.Results || results.results || [];
    const cards = rows.filter(r => r.CanRequestInSeerr || r.canRequestInSeerr).map(r => {
        const badge = {textContent: "", dataset: {}};
        const panel = {textContent: "", innerHTML: ""};
        const card = {querySelector: selector => selector === ".jw-seerr-status" ? badge : panel, badge};
        card.button = {disabled: true, textContent: "", dataset: {seerrId: String(r.TmdbId || r.tmdbId), seerrType: (r.ObjectType || r.objectType) === "SHOW" ? "tv" : "movie"}, closest: () => card};
        return card;
    });
    const elements = new Map();
    const events = {};
    const page = {
        dataset: {},
        querySelector(selector) {
            if (!elements.has(selector)) elements.set(selector, {
                value: selector === '.justWatchTitle' ? 'Arrival' : '',
                focus() { this.focused = true; }, innerHTML: '', textContent: '', disabled: false, options: [{textContent:''}],
                querySelectorAll: selector => selector === '.jw-direct-request' ? cards.map(c => c.button) : [],
                addEventListener(type, handler) { events[selector + type] = handler; }
            });
            return elements.get(selector);
        },
        addEventListener(type, handler) { events[type] = handler; }
    };
    const calls = { updates: [], confirmations: [], searches: [], ajax: [] };
    let source;
    if (host === 'Jellyfin') {
        const html = fs.readFileSync('src/Jellyfin.Plugin.JustWatch/Configuration/configPage.html', 'utf8');
        assert.doesNotMatch(html, /data-controller=/, 'Jellyfin page must initialize without an Emby controller');
        const scripts = [...html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)];
        assert.equal(scripts.length, 1, 'Jellyfin configuration script must be embedded in its page');
        source = scripts[0][1];
    } else {
        source = fs.readFileSync(`src/${host}.Plugin.JustWatch/Configuration/configPage.js`, 'utf8');
    }
    vm.runInNewContext(source, {
        URL, document: { querySelector: () => page }, define: (_dependencies, factory) => factory()(page),
        Dashboard: { showLoadingMsg() {}, hideLoadingMsg() {}, alert() {}, processPluginConfigurationUpdateResult(result) { calls.confirmations.push(result); } },
        ApiClient: {
            getPluginConfiguration: async () => ({ Options: {} }),
            updatePluginConfiguration: async (_id, config) => { calls.updates.push(config); return { status: 204 }; },
            getUrl: (_path, parameters) => { calls.searches.push(parameters); return '/' + _path; },
            ajax: (options) => {
                calls.ajax.push(options);
                if (fail) return Promise.reject(new Error('offline'));
                if (options.type === 'POST') return Promise.resolve({ Success: true, Message: 'Request accepted by Seerr.' });
                if (String(options.url).includes('Seerr/Test')) return Promise.resolve({Success:true,Message:'Connected to Seerr.'});
                if (String(options.url).includes('Seerr/Details')) return details === null ? Promise.reject(new Error('status offline')) : Promise.resolve(details);
                return Promise.resolve(results);
            }
        }
    });
    events.pageshow.call(page);
    await new Promise(resolve => setImmediate(resolve));
    elements.get('.justWatchTitle').value = 'Arrival';
    events['.justWatchSearchButtonclick']();
    await new Promise(resolve => setImmediate(resolve));
    return { result: elements.get('.justWatchResults'), button: elements.get('.justWatchSearchButton'), elements, events, calls, page, cards };
}
for (const host of ['Emby', 'Jellyfin']) {
    test(`${host}: country switching preserves request controls and makes no network calls`, async () => {
        const {events,calls}=await render(host,[]);
        const panels=[{dataset:{countryPanel:'CA'},hidden:false},{dataset:{countryPanel:'US'},hidden:true}];
        const tabs=['CA','US'].map(code=>({dataset:{countrySelect:code},setAttribute(name,value){this[name]=value;}}));
        const card={querySelectorAll:selector=>selector==='[data-country-select]'?tabs:panels};
        tabs.forEach(tab=>tab.closest=()=>card);
        const before=calls.ajax.length;
        events['.justWatchResultsclick']({target:{closest:selector=>selector==='button[data-country-select]'?tabs[1]:null}});
        assert.equal(panels[0].hidden,true);
        assert.equal(panels[1].hidden,false);
        assert.equal(tabs[1]['aria-pressed'],'true');
        assert.equal(calls.ajax.length,before);
    });
    test(`${host}: same exact title across countries renders one card with separate prices and tabs`, async () => {
        const resultRows = [
            {Title:'Arrival',TmdbId:329865,ObjectType:'MOVIE',Country:'CA',CanRequestInSeerr:true,Offers:[{ProviderName:'Example',Price:'4.99',Currency:'CAD'}]},
            {Title:'Arrival',TmdbId:329865,ObjectType:'MOVIE',Country:'US',CanRequestInSeerr:true,Offers:[{ProviderName:'Example',Price:'3.99',Currency:'USD'}]},
            {Title:'Arrival',TmdbId:999,ObjectType:'MOVIE',Country:'CA',Offers:[]}
        ];
        const { result } = await render(host, resultRows);
        assert.equal((result.innerHTML.match(/<article class="jw-result">/g)||[]).length,2,'different TMDB IDs must not merge');
        assert.equal((result.innerHTML.match(/class="jw-source jw-seerr jw-direct-request"/g)||[]).length,1);
        assert.match(result.innerHTML,/data-country-select="CA" aria-pressed="true"/);
        assert.match(result.innerHTML,/data-country-panel="US" hidden/);
        assert.match(result.innerHTML,/4.99 · CAD/);
        assert.match(result.innerHTML,/3.99 · USD/);
    });
    test(`${host}: partial country failures retain successful cards and show escaped warnings`, async () => {
        const { result } = await render(host,{Results:[{Title:'Arrival',Country:'CA',Offers:[]}],FailedCountries:['US','<GB>']});
        assert.match(result.innerHTML,/Could not check US, &lt;GB&gt;/);
        assert.match(result.innerHTML,/Arrival/);
        assert.match(result.innerHTML,/only successful countries/);
    });
    test(`${host}: connection test uses a GET without exposing key or creating requests`, async () => {
        const { elements, events, calls } = await render(host,[]);
        const before = calls.ajax.length;
        await events['.justWatchTestSeerrclick'].call(elements.get('.justWatchTestSeerr'));
        const request = calls.ajax[before];
        assert.equal(request.type,'GET');
        assert.equal(request.url,'/JustWatch/Seerr/Test');
        assert.equal(request.data,undefined);
        assert.equal(elements.get('.justWatchSeerrFeedback').textContent,'Connected to Seerr.');
        assert.equal(calls.ajax.filter(call => call.type==='POST').length,0);
    });
    if(host==='Jellyfin') test('Jellyfin: cleanup requires explicit selection before POST', async () => {
        const {elements,events,calls}=await render(host,[]);
        events['.justWatchCleanupclick'].call(elements.get('.justWatchCleanup'));
        assert.equal(calls.ajax.filter(call=>call.type==='POST').length,0);
        elements.get('.justWatchCleanupConfirm').checked=true;
        events['.justWatchCleanupclick'].call(elements.get('.justWatchCleanup'));
        await new Promise(resolve=>setImmediate(resolve));
        assert.equal(calls.ajax.at(-1).url,'/JustWatch/Maintenance/Cleanup');
        assert.equal(calls.ajax.at(-1).type,'POST');
    });
    test(`${host}: Clear resets the search immediately and focuses title without another request`, async () => {
        const { page, elements, events, calls } = await render(host, [{Title:'Arrival',Offers:[]}]);
        const before = calls.ajax.length;
        elements.get('.justWatchYear').value = '2016';
        events['.justWatchClearButtonclick']();
        assert.equal(elements.get('.justWatchTitle').value,'');
        assert.equal(elements.get('.justWatchYear').value,'');
        assert.equal(elements.get('.justWatchResults').innerHTML,'');
        assert.equal(elements.get('.justWatchTitle').focused,true);
        assert.equal(calls.ajax.length,before);
        assert.equal(page.providerResults.length,0);
    });
    test(`${host}: leaving and reopening clears searches but retains settings and provider choices`, async () => {
        const { page, elements, events } = await render(host, [{Title:'Arrival', Country:'CA', Offers:[]}]);
        elements.get('.justWatchYear').value = '2016';
        elements.get('.justWatchMediaType').value = 'movie';
        page.selectedProviders = new Map([['1','Netflix']]);
        const generation = page.searchGeneration;
        events.pagehide();
        for (const selector of ['.justWatchTitle','.justWatchYear','.justWatchMediaType']) assert.equal(elements.get(selector).value,'');
        assert.equal(elements.get('.justWatchResults').innerHTML,'');
        assert.equal(elements.get('.justWatchProviderFilters').innerHTML,'');
        assert.equal(page.providerResults.length,0);
        assert.ok(page.searchGeneration > generation, 'old in-flight responses are invalidated');
        events.pageshow.call(page);
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(elements.get('.justWatchTitle').value,'');
        assert.equal(elements.get('.justWatchCountry').value,'US');
        assert.equal(page.selectedProviders.get('1'),'Netflix');
    });
    test(`${host}: Anywhere is explicit, passes scope, labels countries and currencies`, async () => {
        const { elements, events, calls, result } = await render(host, [{Title:'Arrival', Country:'CA', Offers:[{ProviderName:'Example',Price:'$4.99',Currency:'CAD'}]}]);
        assert.equal(calls.searches[0].anywhere, true);
        assert.equal(calls.searches[0].country, null);
        const menu = elements.get('.justWatchSearchScope');
        assert.match(menu.innerHTML, /All chosen countries/);
        assert.match(menu.innerHTML, /Canada \(CA\)/);
        assert.match(menu.innerHTML, /United States \(US\)/);
        menu.value = 'CA';
        events['.justWatchSearchButtonclick']();
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(calls.searches.at(-1).anywhere, false);
        assert.equal(calls.searches.at(-1).country, 'CA');
        elements.get('.justWatchSearchScope').value = 'anywhere';
        events['.justWatchSearchButtonclick']();
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(calls.searches.at(-1).anywhere, true);
        assert.match(result.innerHTML, /Availability in CA/);
        assert.match(result.innerHTML, /4.99 · CAD/);
        elements.get('.justWatchAnywhereCountries').value = 'USA';
        events['.justWatchSettingsFormsubmit']({preventDefault(){}});
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(calls.updates.length, 0, 'invalid country list must not save');
    });
    test(`${host}: provider filters hide unrelated offers, preserve title actions, and reset`, async () => {
        const { elements, events, calls, page } = await render(host, [{Title:'Arrival', Offers:[
            {ProviderId:1, ProviderName:'Netflix', MonetizationType:'FLATRATE'},
            {ProviderId:2, ProviderName:'Apple <TV>', MonetizationType:'RENT'}
        ]}]);
        const toolbar = elements.get('.justWatchProviderFilters');
        assert.match(toolbar.innerHTML, /Apple &lt;TV&gt;/);
        const providers = [1,2].map(id => ({dataset:{providerKey:String(id)},hidden:false}));
        const count = {};
        const section = {querySelectorAll:()=>providers,querySelector:()=>count};
        const empty = {hidden:true};
        const card = {querySelectorAll:()=>[section],querySelector:()=>empty};
        elements.get('.justWatchResults').querySelectorAll = () => [card];
        const before = calls.ajax.length;
        const click = dataset => events['.justWatchProviderFiltersclick']({target:{closest:()=>({dataset})}});
        click({providerFilter:'1'});
        assert.equal(providers[0].hidden,false);
        assert.equal(providers[1].hidden,true);
        assert.equal(count.textContent,'1 provider');
        assert.equal(empty.hidden,true);
        assert.match(toolbar.innerHTML, /data-provider-filter="1" aria-pressed="true"/);
        page.selectedProviders = new Map([['3','Other service']]);
        click({providerFilter:'4'});
        assert.equal(section.hidden,true);
        assert.equal(empty.hidden,false);
        click({providerAll:'true'});
        assert.equal(section.hidden,false);
        assert.equal(providers[1].hidden,false);
        assert.equal(empty.hidden,true);
        assert.equal(calls.ajax.length,before,'filtering must not reload Seerr status or submit requests');
    });
    test(`${host}: initializes defaults and saves normalized configuration`, async () => {
        const { elements, events, calls, page } = await render(host, []);
        assert.equal(elements.get('.justWatchCountry').value, 'US');
        assert.equal(elements.get('.justWatchLanguage').value, 'en');
        assert.equal(elements.get('.justWatchResultLimit').value, 8);
        assert.equal(elements.get('.justWatchAnywhereCountries').value, 'CA,US,GB,AU');
        assert.equal(elements.get('.justWatchSearchScope').value, 'anywhere');
        elements.get('.justWatchAnywhereCountries').value = 'ca, US,ca,gb';
        elements.get('.justWatchSeerrApiKey').value = 'test-key';
        elements.get('.justWatchSeerrUrl').value = 'https://seerr.example.com';
        elements.get('.justWatchCountry').value = 'ca';
        elements.get('.justWatchLanguage').value = 'EN';
        elements.get('.justWatchResultLimit').value = '12';
        let prevented = false;
        events['.justWatchSettingsFormsubmit']({ preventDefault() { prevented = true; } });
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(prevented, true);
        assert.equal(calls.updates.length, 1);
        assert.equal(calls.updates[0].Options.SeerrApiKey, 'test-key');
        assert.equal(calls.updates[0].Options.SeerrUrl, 'https://seerr.example.com');
        assert.equal(calls.updates[0].Options.Country, 'CA');
        assert.equal(calls.updates[0].Options.AnywhereCountries, 'CA,US,GB');
        assert.equal(calls.updates[0].Options.Language, 'en');
        assert.equal(calls.updates[0].Options.ResultLimit, 12);
        assert.equal(calls.confirmations[0].status, 204);
        events.pageshow.call(page);
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(calls.searches[0].title, 'Arrival');
    });

    test(`${host}: PascalCase and camelCase offers render with prices and categories`, async () => {
        for (const camel of [true, false]) {
            const offer = { ProviderName: 'Example', MonetizationType: 'RENT', Price: 'CA$ 4.99', Url: 'https://example.com/watch', Quality: 'HD' };
            const item = { Title: 'Arrival', Country: 'CA', Offers: [camel ? lowerKeys(offer) : offer] };
            const { result, button } = await render(host, [camel ? lowerKeys(item) : item]);
            assert.match(result.innerHTML, /Rent/);
            assert.match(result.innerHTML, /CA\$ 4.99/);
            assert.match(result.innerHTML, /https:\/\/example.com\/watch/);
            assert.equal(button.disabled, false);
        }
    });
    test(`${host}: groups provider qualities while preserving different prices and destinations`, async () => {
        const offers = [
            { ProviderId: '1', ProviderName: 'Example', MonetizationType: 'RENT', Price: '$3.99', Quality: 'SD', Url: 'https://example.com/rent' },
            { ProviderId: '1', ProviderName: 'Example', MonetizationType: 'RENT', Price: '$3.99', Quality: 'HD', Url: 'https://example.com/rent' },
            { ProviderId: '1', ProviderName: 'Example', MonetizationType: 'RENT', Price: '$5.99', Quality: '_4K', Url: 'https://example.com/4k' },
            { ProviderId: '2', ProviderName: 'Disc shop', MonetizationType: 'BUY', Price: '$8.99', Quality: 'DVD', Url: 'https://example.com/disc' }
        ];
        const { result } = await render(host, [{ Title: 'Example', Offers: offers }]);
        assert.equal((result.innerHTML.match(/class="jw-provider"/g) || []).length, 2);
        assert.equal((result.innerHTML.match(/class="jw-offer"/g) || []).length, 3);
        assert.match(result.innerHTML, /HD \/ SD/);
        assert.match(result.innerHTML, /\$5.99/);
        assert.match(result.innerHTML, /https:\/\/example.com\/4k/);
        assert.match(result.innerHTML, /Disc &amp; Blu-ray/);
    });
    test(`${host}: exposes safe Seerr title links only when configured`, async () => {
        const { result } = await render(host, [{Title: 'Arrival', SeerrUrl: 'https://seerr.example.com/movie/329865'}]);
        assert.match(result.innerHTML, /Request in Seerr/);
        assert.match(result.innerHTML, /https:\/\/seerr.example.com\/movie\/329865/);
        assert.doesNotMatch((await render(host, [{Title: 'Arrival'}])).result.innerHTML, /Request in Seerr/);
        assert.doesNotMatch((await render(host, [{Title: 'Arrival', SeerrUrl: 'javascript:alert(1)'}])).result.innerHTML, /Request in Seerr/);
    });
    test(`${host}: direct movie request uses POST and never sends the API key`, async () => {
        const view = await render(host, [{Title: 'Arrival', TmdbId: 329865, ObjectType: 'MOVIE', CanRequestInSeerr: true}]);
        assert.match(view.result.innerHTML, /jw-direct-request/);
        const panel = {textContent: ''};
        const badge = {textContent:'',dataset:{}};
        const card = {querySelector: selector => selector === '.jw-seerr-status' ? badge : panel};
        const button = {disabled: false, dataset: {seerrId: '329865', seerrType: 'movie'}, closest: () => card};
        view.events['.justWatchResultsclick']({target: {closest: () => button}});
        await new Promise(resolve => setImmediate(resolve));
        const post = view.calls.ajax.find(c => c.type === 'POST');
        assert.equal(post.url, '/JustWatch/Seerr/Request');
        assert.deepEqual(JSON.parse(post.data), {TmdbId: 329865, MediaType: 'movie', Seasons: []});
        assert.doesNotMatch(post.data, /ApiKey|apiKey/);
        assert.equal(button.disabled, true);
        assert.equal(button.textContent, 'Requested');
        assert.match(panel.textContent, /accepted/);
    });
    test(`${host}: TV loads choices before requesting only selected seasons`, async () => {
        const view = await render(host, [{Title: 'Show', TmdbId: 1396, ObjectType: 'SHOW', CanRequestInSeerr: true}]);
        const panel = {textContent: '', innerHTML: '', querySelectorAll: () => [{value:'2'}]};
        let button;
        const badge = {textContent:'',dataset:{}};
        const card = {querySelector: (selector) => selector === '.jw-request-panel' ? panel : selector === '.jw-seerr-status' ? badge : button};
        button = {disabled: false, dataset: {seerrId: '1396', seerrType: 'tv'}, closest: () => card};
        view.events['.justWatchResultsclick']({target: {closest: () => button}});
        await new Promise(resolve => setImmediate(resolve));
        assert.match(panel.innerHTML, /Season 1/);
        assert.equal(view.calls.ajax.filter(c => c.type === 'POST').length, 0);
        const submit = {disabled: false, dataset: {seerrId: '1396', seerrType: 'tv', seerrAction:'submit'}, closest: () => card};
        view.events['.justWatchResultsclick']({target: {closest: () => submit}});
        await new Promise(resolve => setImmediate(resolve));
        assert.deepEqual(JSON.parse(view.calls.ajax.find(c => c.type === 'POST').data).Seasons, [2]);
    });
    test(`${host}: statuses disable duplicate movie requests but leave unrequested seasons usable`, async () => {
        for (const state of ['available','pending','requested','blocked']) {
            const view = await render(host, [{Title:'Movie',TmdbId:1,ObjectType:'MOVIE',CanRequestInSeerr:true}], false, {State:state,CanRequest:false,Available:state==='available'});
            assert.equal(view.cards[0].button.disabled, true);
            assert.equal(view.cards[0].badge.dataset.state, state);
        }
        const tv = await render(host, [{Title:'Show',TmdbId:2,ObjectType:'SHOW',CanRequestInSeerr:true}], false, {State:'partial',CanRequest:true});
        assert.equal(tv.cards[0].button.disabled, false);
        assert.equal(tv.cards[0].button.textContent, 'Choose seasons');
        assert.equal(tv.calls.ajax.filter(c=>c.type==='POST').length, 0);
    });
    test(`${host}: failed status lookup is visibly unknown and does not claim availability`, async () => {
        const view = await render(host, [{Title:'Movie',TmdbId:1,ObjectType:'MOVIE',CanRequestInSeerr:true}], false, null);
        assert.equal(view.cards[0].badge.textContent, 'Status unavailable');
        assert.equal(view.cards[0].button.disabled, false);
    });
    test(`${host}: renders safe lazy posters and falls back for missing or unsafe URLs`, async () => {
        const view = await render(host, [{Title:'Arrival',PosterUrl:'https://images.justwatch.com/poster/123/s166'},{Title:'Unknown',PosterUrl:'javascript:alert(1)'}]);
        assert.equal((view.result.innerHTML.match(/class="jw-poster"/g)||[]).length, 1);
        assert.match(view.result.innerHTML, /loading="lazy"/);
        assert.doesNotMatch(view.result.innerHTML, /javascript:/);
        assert.match(view.result.innerHTML, /jw-poster-fallback/);
    });
    test(`${host}: escapes titles and rejects script links`, async () => {
        const { result } = await render(host, [{ Title: '<script>alert(1)</script>', JustWatchUrl: 'javascript:alert(1)', Offers: [{ProviderName: '<img onerror=x>', Url: 'javascript:alert(1)'}] }]);
        assert.doesNotMatch(result.innerHTML, /<script>|<img|javascript:/);
        assert.match(result.innerHTML, /&lt;script&gt;/);
    });
    test(`${host}: distinguishes no titles, no offers, and upstream failure`, async () => {
        assert.match((await render(host, [])).result.innerHTML, /No matching titles/);
        assert.match((await render(host, [{ Title: 'Arrival', Offers: [] }])).result.innerHTML, /No streaming offers/);
        const { result, button } = await render(host, [], true);
        assert.match(result.textContent, /lookup failed/);
        assert.equal(button.disabled, false);
    });
}
function lowerKeys(object) {
    return Object.fromEntries(Object.entries(object).map(([k, v]) => [k[0].toLowerCase() + k.slice(1), v]));
}
