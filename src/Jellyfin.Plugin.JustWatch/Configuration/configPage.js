define([], function () {
    const pluginId = 'fd91b02d-7551-46f1-a205-2ef7607c25d8';

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
        }).then(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.processPluginConfigurationUpdateResult();
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

    function renderResults(page, results) {
        const container = query(page, '.justWatchResults');
        if (!results || !results.length) {
            container.innerHTML = '<p>No matching titles found. Try another title or omit the year.</p>';
            return;
        }
        container.innerHTML = results.map(function (raw) {
            // Jellyfin normally uses camelCase; Emby uses PascalCase.
            const item = {};
            Object.keys(raw).forEach(function (key) { item[key.toLowerCase()] = raw[key]; });
            const groups = {};
            (item.offers || []).forEach(function (rawOffer) {
                const offer = {};
                Object.keys(rawOffer).forEach(function (key) { offer[key.toLowerCase()] = rawOffer[key]; });
                const type = String(offer.monetizationtype || '').toUpperCase();
                const label = { FLATRATE: 'Subscription', RENT: 'Rent', BUY: 'Buy', FREE: 'Free', ADS: 'Free with ads' }[type] || 'Other offers';
                (groups[label] = groups[label] || []).push(offer);
            });
            const offers = Object.keys(groups).map(function (label) {
                return '<h4>' + escapeHtml(label) + '</h4><ul>' + groups[label].map(function (offer) {
                    const href = safeUrl(offer.url);
                    return '<li>' + (href ? '<a target="_blank" rel="noopener noreferrer" href="' + href + '">' : '')
                        + escapeHtml(offer.providername) + (href ? '</a>' : '')
                        + (offer.quality ? ' · ' + escapeHtml(offer.quality) : '')
                        + (offer.price ? ' · ' + escapeHtml(offer.price) : '') + '</li>';
                }).join('') + '</ul>';
            }).join('') || '<p>No streaming offers returned for this region. Check JustWatch for details.</p>';
            const href = safeUrl(item.justwatchurl);
            return '<div class="paperList" style="padding:1em;margin-bottom:.75em;">'
                + '<h3 style="margin-top:0;">' + escapeHtml(item.title)
                + (item.year ? ' (' + escapeHtml(item.year) + ')' : '') + '</h3>'
                + '<p>Availability in ' + escapeHtml(item.country) + '</p>' + offers
                + (href ? '<a target="_blank" rel="noopener noreferrer" href="' + href + '">Check on JustWatch</a>' : '')
                + '</div>';
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
