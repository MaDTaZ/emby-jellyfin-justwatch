const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

async function render(host, results, fail = false) {
    const elements = new Map();
    const events = {};
    const page = {
        dataset: {},
        querySelector(selector) {
            if (!elements.has(selector)) elements.set(selector, {
                value: selector === '.justWatchTitle' ? 'Arrival' : '',
                innerHTML: '', textContent: '', disabled: false,
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
                if (String(options.url).includes('Seerr/Details')) return Promise.resolve({ Seasons: [{Number: 1, Name: 'Season 1'}, {Number: 2, Name: 'Season 2'}] });
                return Promise.resolve(results);
            }
        }
    });
    events.pageshow.call(page);
    await new Promise(resolve => setImmediate(resolve));
    events['.justWatchSearchButtonclick']();
    await new Promise(resolve => setImmediate(resolve));
    return { result: elements.get('.justWatchResults'), button: elements.get('.justWatchSearchButton'), elements, events, calls, page };
}
for (const host of ['Emby', 'Jellyfin']) {
    test(`${host}: initializes defaults and saves normalized configuration`, async () => {
        const { elements, events, calls, page } = await render(host, []);
        assert.equal(elements.get('.justWatchCountry').value, 'US');
        assert.equal(elements.get('.justWatchLanguage').value, 'en');
        assert.equal(elements.get('.justWatchResultLimit').value, 8);
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
        const card = {querySelector: () => panel};
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
        const card = {querySelector: (selector) => selector === '.jw-request-panel' ? panel : button};
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
