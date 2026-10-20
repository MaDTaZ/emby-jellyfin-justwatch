define([], function () {
    const pluginId = '4df372d4-8179-4f05-9312-419434bd835d';

    function query(page, selector) {
        return page.querySelector(selector);
    }

    function escapeHtml(value) {
        return String(value || '').replace(/[&<>"']/g, function (char) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[char];
        });
    }

    function loadConfig(page) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.Options = config.Options || {};
            query(page, '.justWatchCountry').value = config.Options.Country || 'US';
            query(page, '.justWatchLanguage').value = config.Options.Language || 'en';
            query(page, '.justWatchResultLimit').value = config.Options.ResultLimit || 8;
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Could not load JustWatch settings.');
        });
    }

    function saveConfig(page) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.Options = config.Options || {};
            config.Options.Country = query(page, '.justWatchCountry').value.trim().toUpperCase() || 'US';
            config.Options.Language = query(page, '.justWatchLanguage').value.trim().toLowerCase() || 'en';
            config.Options.ResultLimit = parseInt(query(page, '.justWatchResultLimit').value, 10) || 8;

            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            Dashboard.hideLoadingMsg();
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Could not save JustWatch settings.');
        });
    }

    function safeUrl(value) {
        try {
            const url = new URL(value);
            return /^https?:$/.test(url.protocol) ? escapeHtml(url.href) : '';
        } catch (_) { return ''; }
    }

    function normalize(raw) {
        const value = {};
        Object.keys(raw).forEach(function (key) { value[key.toLowerCase()] = raw[key]; });
        return value;
    }

    function renderResults(page, results) {
        const container = query(page, '.justWatchResults');
        if (!results || !results.length) {
            container.innerHTML = '<p class="jw-empty">No matching titles found. Try another title or omit the year.</p>';
            return;
        }
        const categories = ['Subscription', 'Free', 'Free with ads', 'Rent', 'Buy', 'Disc & Blu-ray', 'Other offers'];
        container.innerHTML = results.map(function (raw) {
            const item = normalize(raw);
            const groups = new Map();
            (item.offers || []).forEach(function (rawOffer) {
                const offer = normalize(rawOffer);
                const type = String(offer.monetizationtype || '').toUpperCase();
                const quality = String(offer.quality || '').replace(/^_/, '').replace('BLURAY', 'Blu-ray');
                const label = /^(DVD|Blu-ray)$/i.test(quality) ? 'Disc & Blu-ray'
                    : ({ FLATRATE: 'Subscription', RENT: 'Rent', BUY: 'Buy', FREE: 'Free', ADS: 'Free with ads' }[type] || 'Other offers');
                if (!groups.has(label)) groups.set(label, new Map());
                const providers = groups.get(label);
                const key = String(offer.providerid || offer.providername || 'Unknown provider');
                if (!providers.has(key)) providers.set(key, { name: offer.providername || 'Unknown provider', variants: new Map() });
                const provider = providers.get(key);
                const href = safeUrl(offer.url);
                // Merge qualities only when both price and destination match.
                const variantKey = JSON.stringify([offer.price || '', href]);
                if (!provider.variants.has(variantKey)) provider.variants.set(variantKey, { price: offer.price, href: href, qualities: new Set() });
                if (quality) provider.variants.get(variantKey).qualities.add(quality);
            });
            const offers = categories.filter(function (label) { return groups.has(label); }).map(function (label) {
                const providers = Array.from(groups.get(label).values());
                return '<section class="jw-section"><div class="jw-section-heading"><h4>' + escapeHtml(label)
                    + '</h4><span class="jw-count">' + providers.length + (providers.length === 1 ? ' provider' : ' providers') + '</span></div>'
                    + '<div class="jw-provider-grid">' + providers.map(function (provider) {
                        return '<article class="jw-provider"><div class="jw-provider-heading"><span class="jw-provider-mark" aria-hidden="true">'
                            + escapeHtml(provider.name.charAt(0).toUpperCase()) + '</span><h5>' + escapeHtml(provider.name) + '</h5></div>'
                            + '<div class="jw-variants">' + Array.from(provider.variants.values()).map(function (variant) {
                                const qualities = Array.from(variant.qualities).sort(function (a, b) {
                                    return ['4K', 'HD', 'SD'].indexOf(a) - ['4K', 'HD', 'SD'].indexOf(b);
                                });
                                const content = '<span class="jw-quality">' + escapeHtml(qualities.join(' / ') || 'View offer') + '</span>'
                                    + '<span class="jw-price">' + escapeHtml(variant.price || 'Watch')
                                    + (variant.href ? ' <span aria-hidden="true">↗</span>' : '') + '</span>';
                                return variant.href ? '<a class="jw-offer" target="_blank" rel="noopener noreferrer" aria-label="'
                                    + escapeHtml(provider.name + ', ' + label + ', ' + (variant.price || 'view offer') + ', ' + qualities.join(' / '))
                                    + '" href="' + variant.href + '">' + content + '</a>' : '<div class="jw-offer jw-unlinked">' + content + '</div>';
                            }).join('') + '</div></article>';
                    }).join('') + '</div></section>';
            }).join('') || '<p class="jw-empty">No streaming offers returned for this region. Check JustWatch for details.</p>';
            const href = safeUrl(item.justwatchurl);
            return '<article class="jw-result"><header class="jw-result-header"><div><p class="jw-eyebrow">Availability in '
                + escapeHtml(item.country) + '</p><h3>' + escapeHtml(item.title)
                + (item.year ? ' <span class="jw-year">' + escapeHtml(item.year) + '</span>' : '') + '</h3></div>'
                + (href ? '<a class="jw-source" target="_blank" rel="noopener noreferrer" href="' + href + '">JustWatch <span aria-hidden="true">↗</span></a>' : '')
                + '</header>' + offers + '<p class="jw-note">Availability and prices can change. Confirm details with the provider.</p></article>';
        }).join('');
    }

    function search(page) {
        const title = query(page, '.justWatchTitle').value.trim();
        if (!title) {
            Dashboard.alert('Enter a title to search.');
            return;
        }

        const year = query(page, '.justWatchYear').value.trim();
        const mediaType = query(page, '.justWatchMediaType').value;
        const url = ApiClient.getUrl('JustWatch/Search', {
            title: title,
            year: year || null,
            mediaType: mediaType || null
        });

        query(page, '.justWatchSearchButton').disabled = true;
        query(page, '.justWatchResults').innerHTML = '<p>Checking availability…</p>';
        Dashboard.showLoadingMsg();
        ApiClient.ajax({ type: 'GET', url: url, dataType: 'json' }).then(function (results) {
            query(page, '.justWatchSearchButton').disabled = false;
            renderResults(page, results);
            Dashboard.hideLoadingMsg();
        }, function () {
            Dashboard.hideLoadingMsg();
            query(page, '.justWatchSearchButton').disabled = false;
            query(page, '.justWatchResults').textContent = 'Availability lookup failed. Try again later and check your saved country and language settings.';
        });
    }

    return function (view) {
        view.addEventListener('pageshow', function () {
            const page = this;
            loadConfig(page);
            if (page.dataset.justWatchBound) {
                return;
            }

            page.dataset.justWatchBound = 'true';
            query(page, '.justWatchSettingsForm').addEventListener('submit', function (event) {
                event.preventDefault();
                saveConfig(page);
            });
            query(page, '.justWatchSearchButton').addEventListener('click', function () {
                search(page);
            });
        });
    };
});
