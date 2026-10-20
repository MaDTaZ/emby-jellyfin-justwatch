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
    vm.runInNewContext(fs.readFileSync(`src/${host}.Plugin.JustWatch/Configuration/configPage.js`, 'utf8'), {
        URL, define: (_dependencies, factory) => factory()(page),
        Dashboard: { showLoadingMsg() {}, hideLoadingMsg() {}, alert() {} },
        ApiClient: {
            getPluginConfiguration: async () => ({ Options: {} }),
            getUrl: () => '/JustWatch/Search',
            ajax: () => fail ? Promise.reject(new Error('offline')) : Promise.resolve(results)
        }
    });
    events.pageshow.call(page);
    events['.justWatchSearchButtonclick']();
    await new Promise(resolve => setImmediate(resolve));
    return { result: elements.get('.justWatchResults'), button: elements.get('.justWatchSearchButton') };
}
for (const host of ['Emby', 'Jellyfin']) {
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
