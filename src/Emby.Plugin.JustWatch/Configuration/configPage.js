define([], function () {
    const pluginId = '4df372d4-8179-4f05-9312-419434bd835d';

    function query(page, selector) {
        return page.querySelector(selector);
    }

    function escapeHtml(value) {
        return String(value == null ? '' : value).replace(/[&<>"']/g, function (char) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[char];
        });
    }

    function populateSearchRegions(page, countries, reset) {
        const select = query(page, '.justWatchSearchScope');
        const previous = reset ? 'anywhere' : select.value;
        const codes = Array.from(new Set(String(countries || 'CA,US,GB,AU').split(',').map(function (code) { return code.trim().toUpperCase(); }))).filter(function (code) { return /^[A-Z]{2}$/.test(code); });
        const names = { CA: 'Canada', US: 'United States', GB: 'United Kingdom', AU: 'Australia', FR: 'France', DE: 'Germany', IT: 'Italy', ES: 'Spain', NZ: 'New Zealand', IE: 'Ireland', IN: 'India', JP: 'Japan' };
        select.innerHTML = '<option value="anywhere">All chosen countries</option>' + codes.map(function (code) {
            return '<option value="' + code + '">' + escapeHtml(names[code] ? names[code] + ' (' + code + ')' : code) + '</option>';
        }).join('');
        select.value = codes.indexOf(previous) >= 0 ? previous : 'anywhere';
    }

    const settingFields = ['.justWatchCountry', '.justWatchAnywhereCountries', '.justWatchLanguage', '.justWatchResultLimit', '.justWatchSeerrUrl', '.justWatchSeerrApiKey'];

    function loadConfig(page) {
        if (page.settingsLoaded || page.settingsLoading) return;
        page.settingsLoading = true;
        page.settingsEdited = page.settingsEdited || new Set();
        function setField(selector, value) {
            if (!page.settingsEdited.has(selector)) query(page, selector).value = value;
        }
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.Options = config.Options || {};
            setField('.justWatchCountry', config.Options.Country || 'US');
            setField('.justWatchAnywhereCountries', config.Options.AnywhereCountries || 'CA,US,GB,AU');
            populateSearchRegions(page, query(page, '.justWatchAnywhereCountries').value, true);
            setField('.justWatchSeerrApiKey', config.Options.SeerrApiKey || '');
            setField('.justWatchSeerrUrl', config.Options.SeerrUrl || '');
            setField('.justWatchLanguage', config.Options.Language || 'en');
            setField('.justWatchResultLimit', config.Options.ResultLimit || 8);
            page.settingsLoading = false;
            page.settingsLoaded = true;
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            page.settingsLoading = false;
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Could not load Where to Watch settings.');
        });
    }

    function saveConfig(page) {
        const countries = Array.from(new Set(query(page, '.justWatchAnywhereCountries').value.toUpperCase().split(',').map(function (value) { return value.trim(); })));
        if (countries.length > 6 || countries.some(function (value) { return !/^[A-Z]{2}$/.test(value); })) {
            Dashboard.alert('Choose one to six comma-separated two-letter country codes for Anywhere, such as CA,US,GB,AU.');
            return;
        }
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.Options = config.Options || {};
            config.Options.Country = query(page, '.justWatchCountry').value.trim().toUpperCase() || 'US';
            config.Options.AnywhereCountries = countries.join(',');
            config.Options.Language = query(page, '.justWatchLanguage').value.trim().toLowerCase() || 'en';
            config.Options.SeerrApiKey = query(page, '.justWatchSeerrApiKey').value.trim();
            config.Options.SeerrUrl = query(page, '.justWatchSeerrUrl').value.trim();
            config.Options.ResultLimit = parseInt(query(page, '.justWatchResultLimit').value, 10) || 8;

            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            Dashboard.hideLoadingMsg();
            populateSearchRegions(page, countries.join(','), false);
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Could not save Where to Watch settings.');
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

    function statusLabel(state) {
        return { available: 'Available', pending: 'Pending approval', requested: 'Requested', partial: 'Partially available', blocked: 'Blocked in Seerr', notRequested: 'Not requested' }[state] || 'Status unavailable';
    }

    function setStatus(button, state) {
        const badge = button.closest('.jw-result').querySelector('.jw-seerr-status');
        badge.textContent = statusLabel(state);
        badge.dataset.state = state;
    }

    function applySeerrDetails(button, raw) {
        const details = normalize(raw);
        if (details.errormessage) throw new Error('Seerr status unavailable');
        const state = details.state || (details.available ? 'available' : 'notRequested');
        setStatus(button, state);
        button.disabled = details.canrequest === false || details.available === true;
        button.textContent = button.disabled ? (state === 'partial' || state === 'notRequested' ? 'No new seasons' : statusLabel(state))
            : button.dataset.seerrType === 'tv' ? 'Choose seasons' : 'Request';
    }

    function refreshSeerrStatuses(page) {
        const generation = (page.seerrStatusGeneration || 0) + 1;
        page.seerrStatusGeneration = generation;
        const buttons = Array.from(query(page, '.justWatchResults').querySelectorAll('.jw-direct-request'));
        let next = 0;
        async function worker() {
            while (next < buttons.length && generation === page.seerrStatusGeneration) {
                const button = buttons[next++];
                try {
                    const result = await ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('JustWatch/Seerr/Details', {
                        tmdbId: Number(button.dataset.seerrId), mediaType: button.dataset.seerrType
                    }), dataType: 'json' });
                    if (generation !== page.seerrStatusGeneration || button.isConnected === false) continue;
                    applySeerrDetails(button, result);
                } catch (_) {
                    if (generation !== page.seerrStatusGeneration || button.isConnected === false) continue;
                    setStatus(button, 'unknown');
                    button.disabled = false;
                    button.textContent = button.dataset.seerrType === 'tv' ? 'Choose seasons' : 'Request';
                }
            }
        }
        // Keep status lookups responsive without flooding Seerr for larger searches.
        for (let i = 0; i < Math.min(3, buttons.length); i++) worker();
    }

    function providerKey(offer) {
        return String(offer.providerid || offer.providername || 'Unknown provider');
    }

    function loadProviderFilters(page) {
        try {
            const saved = JSON.parse(localStorage.getItem('whereToWatch.providers') || '[]');
            page.selectedProviders = new Map(Array.isArray(saved) ? saved.filter(function (entry) {
                return Array.isArray(entry) && entry.length === 2 && typeof entry[0] === 'string' && typeof entry[1] === 'string';
            }) : []);
        } catch (_) { page.selectedProviders = new Map(); }
    }

    function renderProviderFilters(page, results) {
        const providers = new Map(page.selectedProviders);
        (results || []).forEach(function (raw) {
            (normalize(raw).offers || []).forEach(function (rawOffer) {
                const offer = normalize(rawOffer);
                providers.set(providerKey(offer), String(offer.providername || 'Unknown provider'));
            });
        });
        page.providerNames = providers;
        const selected = page.selectedProviders;
        query(page, '.justWatchProviderFilters').innerHTML = providers.size ? '<div class="jw-filter-heading">Filter providers <span>Choose services to show. Your choices are remembered in this browser.</span></div><div class="jw-filter-options">'
            + '<button type="button" class="jw-filter" data-provider-all="true" aria-pressed="' + !selected.size + '">All providers</button>'
            + Array.from(providers).sort(function (a, b) { return a[1].localeCompare(b[1]); }).map(function (entry) {
                return '<button type="button" class="jw-filter" data-provider-filter="' + escapeHtml(entry[0]) + '" aria-pressed="' + selected.has(entry[0]) + '">' + escapeHtml(entry[1]) + '</button>';
            }).join('') + '</div>' : '';
    }

    function applyProviderFilters(page) {
        const selected = page.selectedProviders;
        const container = query(page, '.justWatchResults');
        container.querySelectorAll('.jw-result').forEach(function (card) {
            let visible = 0;
            card.querySelectorAll('.jw-section').forEach(function (section) {
                let count = 0;
                section.querySelectorAll('[data-provider-key]').forEach(function (provider) {
                    provider.hidden = selected.size > 0 && !selected.has(provider.dataset.providerKey);
                    if (!provider.hidden) count++;
                });
                section.hidden = count === 0;
                section.querySelector('.jw-count').textContent = count + (count === 1 ? ' provider' : ' providers');
                const panel = section.closest ? section.closest('.jw-country-panel') : null;
                if (!panel || !panel.hidden) visible += count;
            });
            card.querySelector('.jw-filter-empty').hidden = !selected.size || visible > 0;
        });
    }

    function handleProviderFilter(page, event) {
        const button = event.target.closest('button[data-provider-filter], button[data-provider-all]');
        if (!button) return;
        if (button.dataset.providerAll) page.selectedProviders.clear();
        else {
            const key = button.dataset.providerFilter;
            if (page.selectedProviders.has(key)) page.selectedProviders.delete(key);
            else page.selectedProviders.set(key, page.providerNames.get(key) || key);
        }
        try { localStorage.setItem('whereToWatch.providers', JSON.stringify(Array.from(page.selectedProviders))); } catch (_) { }
        renderProviderFilters(page, page.providerResults);
        applyProviderFilters(page);
    }

    function renderCountryOffers(item) {
        const categories = ['Subscription', 'Free', 'Free with ads', 'Rent', 'Buy', 'Disc & Blu-ray', 'Other offers'];
            const groups = new Map();
            (item.offers || []).forEach(function (rawOffer) {
                const offer = normalize(rawOffer);
                const type = String(offer.monetizationtype || '').toUpperCase();
                const quality = String(offer.quality || '').replace(/^_/, '').replace('BLURAY', 'Blu-ray');
                const label = /^(DVD|Blu-ray)$/i.test(quality) ? 'Disc & Blu-ray'
                    : ({ FLATRATE: 'Subscription', RENT: 'Rent', BUY: 'Buy', FREE: 'Free', ADS: 'Free with ads' }[type] || 'Other offers');
                if (!groups.has(label)) groups.set(label, new Map());
                const providers = groups.get(label);
                const key = providerKey(offer);
                if (!providers.has(key)) providers.set(key, { key: key, name: offer.providername || 'Unknown provider', variants: new Map() });
                const provider = providers.get(key);
                const href = safeUrl(offer.url);
                // Merge qualities only when both price and destination match.
                const variantKey = JSON.stringify([offer.price || '', offer.currency || '', href]);
                if (!provider.variants.has(variantKey)) provider.variants.set(variantKey, { price: offer.price, currency: offer.currency, href: href, qualities: new Set() });
                if (quality) provider.variants.get(variantKey).qualities.add(quality);
            });
            const offers = categories.filter(function (label) { return groups.has(label); }).map(function (label) {
                const providers = Array.from(groups.get(label).values());
                return '<section class="jw-section"><div class="jw-section-heading"><h4>' + escapeHtml(label)
                    + '</h4><span class="jw-count">' + providers.length + (providers.length === 1 ? ' provider' : ' providers') + '</span></div>'
                    + '<div class="jw-provider-grid">' + providers.map(function (provider) {
                        return '<article class="jw-provider" data-provider-key="' + escapeHtml(provider.key) + '"><div class="jw-provider-heading"><span class="jw-provider-mark" aria-hidden="true">'
                            + escapeHtml(provider.name.charAt(0).toUpperCase()) + '</span><h5>' + escapeHtml(provider.name) + '</h5></div>'
                            + '<div class="jw-variants">' + Array.from(provider.variants.values()).map(function (variant) {
                                const qualities = Array.from(variant.qualities).sort(function (a, b) {
                                    return ['4K', 'HD', 'SD'].indexOf(a) - ['4K', 'HD', 'SD'].indexOf(b);
                                });
                                const content = '<span class="jw-quality">' + escapeHtml(qualities.join(' / ') || 'View offer') + '</span>'
                                    + '<span class="jw-price">' + escapeHtml(variant.price ? variant.price + (variant.currency ? ' · ' + variant.currency : '') : 'Watch')
                                    + (variant.href ? ' <span aria-hidden="true">↗</span>' : '') + '</span>';
                                return variant.href ? '<a class="jw-offer" target="_blank" rel="noopener noreferrer" aria-label="'
                                    + escapeHtml(provider.name + ', ' + label + ', ' + (variant.price || 'view offer') + ', ' + qualities.join(' / '))
                                    + '" href="' + variant.href + '">' + content + '</a>' : '<div class="jw-offer jw-unlinked">' + content + '</div>';
                            }).join('') + '</div></article>';
                    }).join('') + '</div></section>';
            }).join('') || '<p class="jw-empty">No streaming offers returned for this region. Check JustWatch for details.</p>';
        return offers;
    }

    function groupCountryResults(results) {
        const groups = new Map();
        results.forEach(function (raw, index) {
            const item = normalize(raw);
            const type = String(item.objecttype || '').toUpperCase();
            const key = Number(item.tmdbid) > 0 ? type + ':tmdb:' + Number(item.tmdbid) : item.id ? type + ':jw:' + item.id : 'unmatched:' + index;
            if (!groups.has(key)) groups.set(key, Object.assign({}, item, { regions: [] }));
            const group = groups.get(key);
            if (!group.regions.some(function (region) { return region.country === item.country; })) group.regions.push(item);
        });
        return Array.from(groups.values());
    }

    function selectCountry(page, event) {
        const button = event.target.closest('button[data-country-select]');
        if (!button || !button.dataset.countrySelect) return;
        const card = button.closest('.jw-result');
        card.querySelectorAll('[data-country-select]').forEach(function (tab) { tab.setAttribute('aria-pressed', String(tab === button)); });
        card.querySelectorAll('.jw-country-panel').forEach(function (panel) { panel.hidden = panel.dataset.countryPanel !== button.dataset.countrySelect; });
        applyProviderFilters(page);
    }

    function runSettingsCheck(page, button, path, method, feedback) {
        button.disabled = true;
        const target = query(page, feedback);
        target.textContent = 'Checking…';
        return ApiClient.ajax({ type: method, url: ApiClient.getUrl(path), dataType: 'json' }).then(function (raw) {
            const result = normalize(raw || {});
            if (typeof result.success !== 'boolean') throw new Error('Invalid response');
            target.textContent = result.message || 'The check finished.';
        }).catch(function (error) {
            const status = Number(error && error.status);
            target.textContent = status === 401 || status === 403 ? 'The server refused this check. Sign in with a server administrator account.'
                : status === 404 ? 'This check endpoint is unavailable. Update the plugin and restart the server.'
                : 'The server did not return a valid check response' + (status ? ' (HTTP ' + status + ')' : '') + '. Update the plugin, restart, and check the server log if it continues.';
        }).then(function () { button.disabled = false; });
    }

    function renderResults(page, results) {
        const response = Array.isArray(results) ? {results: results} : normalize(results || {});
        results = response.results || [];
        const failed = response.failedcountries || [];
        const warning = failed.length ? '<p class="jw-warning" role="status">Could not check ' + escapeHtml(failed.join(', ')) + '. Results below cover only successful countries. Try searching the unavailable countries again.</p>' : '';
        page.providerResults = results || [];
        renderProviderFilters(page, results);
        const container = query(page, '.justWatchResults');
        if (!results || !results.length) {
            container.innerHTML = warning + '<p class="jw-empty">No matching titles found. Try another title or omit the year.</p>';
            return;
        }
        container.innerHTML = warning + groupCountryResults(results).map(function (raw) {
            const item = normalize(raw);
            const offers = (item.regions.length > 1 ? '<div class="jw-country-tabs" aria-label="Offer country">' + item.regions.map(function (region, index) {
                return '<button type="button" class="jw-filter" data-country-select="' + escapeHtml(region.country) + '" aria-pressed="' + (index === 0) + '">' + escapeHtml(region.country) + '</button>';
            }).join('') + '</div>' : '') + item.regions.map(function (region, index) {
                const link = safeUrl(region.justwatchurl);
                return '<div class="jw-country-panel" data-country-panel="' + escapeHtml(region.country) + '"' + (index ? ' hidden' : '') + '><p class="jw-region-label">Offers in ' + escapeHtml(region.country)
                    + (link ? ' · <a class="jw-region-link" target="_blank" rel="noopener noreferrer" href="' + link + '">Check JustWatch ↗</a>' : '') + '</p>' + renderCountryOffers(region) + '</div>';
            }).join('');
            const href = safeUrl(item.justwatchurl);
            const seerrHref = safeUrl(item.seerrurl);
            const type = String(item.objecttype || '').toUpperCase() === 'SHOW' ? 'tv' : 'movie';
            const canRequest = item.canrequestinseerr && Number.isInteger(Number(item.tmdbid)) && Number(item.tmdbid) > 0;
            const poster = safeUrl(item.posterurl);
            return '<article class="jw-result"><header class="jw-result-header"><div class="jw-heading-content"><div class="jw-poster-wrap"><span class="jw-poster-fallback" aria-hidden="true">' + escapeHtml(String(item.title || '?').charAt(0)) + '</span>'
                + (poster ? '<img class="jw-poster" src="' + poster + '" alt="Poster for ' + escapeHtml(item.title) + '" loading="lazy" decoding="async" referrerpolicy="no-referrer">' : '') + '</div><div class="jw-title-content"><p class="jw-eyebrow">Availability in '
                + escapeHtml(item.regions.map(function (region) { return region.country; }).join(' · ')) + '</p><h3>' + escapeHtml(item.title)
                + (item.year ? ' <span class="jw-year">' + escapeHtml(item.year) + '</span>' : '') + '</h3>' + (canRequest ? '<span class="jw-seerr-status" role="status">Checking Seerr…</span>' : '') + '</div></div>'
                + '<div class="jw-actions">'
                + (canRequest ? '<button type="button" class="jw-source jw-seerr jw-direct-request" disabled data-seerr-id="' + Number(item.tmdbid) + '" data-seerr-type="' + type + '">' + (type === 'tv' ? 'Choose seasons' : 'Request') + '</button>' : '')
                + (seerrHref ? '<a class="jw-source jw-seerr" target="_blank" rel="noopener noreferrer" href="' + seerrHref + '">' + (canRequest ? 'Open in Seerr' : 'Request in Seerr') + ' <span aria-hidden="true">↗</span></a>' : '')
                + '</div></header><div class="jw-request-panel" role="status" aria-live="polite"></div>' + offers + '<p class="jw-empty jw-filter-empty" hidden>No offers from your selected providers for this title. Choose All providers to see other options, or request it through Seerr.</p><p class="jw-note">Availability and prices can change. Confirm details with the provider.</p></article>';
        }).join('');
        applyProviderFilters(page);
        refreshSeerrStatuses(page);
    }

    function requestFeedback(button, panel, promise) {
        button.disabled = true;
        panel.textContent = 'Sending request to Seerr…';
        return promise.then(function (raw) {
            const result = normalize(raw);
            panel.textContent = result.message || (result.success ? 'Requested in Seerr.' : 'Seerr could not complete the request.');
            if (result.success || result.state) {
                const state = result.state || 'requested';
                setStatus(button, state);
                button.disabled = button.dataset.seerrType !== 'tv' || state === 'available' || state === 'blocked';
                button.textContent = button.disabled ? statusLabel(state) : 'Choose seasons';
                return;
            }
            button.disabled = false;
        }).catch(function () {
            button.disabled = false;
            panel.textContent = 'Could not request this title. Check your Seerr URL and API key, and whether it is already requested.';
        });
    }

    function sendSeerrRequest(button, panel, id, type, seasons) {
        return requestFeedback(button, panel, ApiClient.ajax({
            type: 'POST', url: ApiClient.getUrl('JustWatch/Seerr/Request'),
            contentType: 'application/json', dataType: 'json',
            data: JSON.stringify({ TmdbId: id, MediaType: type, Seasons: seasons || [] })
        }));
    }

    function handleSeerrClick(page, event) {
        const button = event.target.closest('button[data-seerr-id]');
        if (!button || button.disabled) return;
        const card = button.closest('.jw-result');
        const panel = card.querySelector('.jw-request-panel');
        const id = Number(button.dataset.seerrId);
        const type = button.dataset.seerrType;
        if (button.dataset.seerrAction === 'submit') {
            const seasons = Array.from(panel.querySelectorAll('input[type="checkbox"]:checked')).map(function (input) { return Number(input.value); });
            if (!seasons.length) { Dashboard.alert('Choose at least one season.'); return; }
            const mainButton = card.querySelector('.jw-direct-request');
            // Keep the season selector disabled after successful submission.
            button.disabled = true;
            return sendSeerrRequest(mainButton, panel, id, type, seasons);
        }
        if (type === 'movie') return sendSeerrRequest(button, panel, id, type, []);
        button.disabled = true;
        panel.textContent = 'Loading seasons from Seerr…';
        return ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('JustWatch/Seerr/Details', { tmdbId: id, mediaType: 'tv' }), dataType: 'json' }).then(function (raw) {
            const details = normalize(raw);
            button.disabled = false;
            if (details.errormessage) { panel.textContent = details.errormessage; return; }
            applySeerrDetails(button, details);
            if (details.available || details.canrequest === false) { panel.textContent = 'No new seasons to request. Check the status in Seerr.'; return; }
            const seasons = details.seasons || [];
            if (!seasons.length) { panel.textContent = 'No seasons found. Open this title in Seerr to check it.'; return; }
            panel.innerHTML = '<p>Choose seasons to request</p><div class="jw-season-grid">' + seasons.map(function (rawSeason) {
                const season = normalize(rawSeason);
                return '<label class="jw-season"><input type="checkbox" value="' + escapeHtml(season.number) + '"' + (season.canrequest === false ? ' disabled' : '') + '> ' + escapeHtml(season.name) + (season.state && season.state !== 'notRequested' ? ' · ' + escapeHtml(statusLabel(season.state)) : '') + '</label>';
            }).join('') + '</div><button type="button" class="jw-source jw-seerr" data-seerr-action="submit" data-seerr-id="' + id + '" data-seerr-type="tv">Request selected seasons</button>';
        }).catch(function () {
            button.disabled = false;
            panel.textContent = 'Could not load seasons. Check your Seerr URL and API key.';
        });
    }

    function clearSearch(page) {
        page.searchGeneration = (page.searchGeneration || 0) + 1;
        page.seerrStatusGeneration = (page.seerrStatusGeneration || 0) + 1;
        page.providerResults = [];
        query(page, '.justWatchTitle').value = '';
        query(page, '.justWatchYear').value = '';
        query(page, '.justWatchMediaType').value = '';
        query(page, '.justWatchResults').innerHTML = '';
        query(page, '.justWatchProviderFilters').innerHTML = '';
        query(page, '.justWatchSearchButton').disabled = false;
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
            mediaType: mediaType || null,
            anywhere: query(page, '.justWatchSearchScope').value === 'anywhere',
            country: query(page, '.justWatchSearchScope').value === 'anywhere' ? null : query(page, '.justWatchSearchScope').value
        });

        const generation = (page.searchGeneration || 0) + 1;
        page.searchGeneration = generation;
        query(page, '.justWatchProviderFilters').innerHTML = '';
        query(page, '.justWatchSearchButton').disabled = true;
        query(page, '.justWatchResults').innerHTML = '<p>' + (query(page, '.justWatchSearchScope').value === 'anywhere' ? 'Checking your chosen countries…' : 'Checking availability…') + '</p>';
        Dashboard.showLoadingMsg();
        ApiClient.ajax({ type: 'GET', url: url, dataType: 'json' }).then(function (results) {
            if (generation !== page.searchGeneration) return;
            query(page, '.justWatchSearchButton').disabled = false;
            renderResults(page, results);
            Dashboard.hideLoadingMsg();
        }, function () {
            if (generation !== page.searchGeneration) return;
            Dashboard.hideLoadingMsg();
            query(page, '.justWatchSearchButton').disabled = false;
            query(page, '.justWatchResults').textContent = 'Availability lookup failed. Try again later and check your saved country and language settings.';
        });
    }

    return function (view) {
        view.addEventListener('pageshow', function () {
            const page = this;
            clearSearch(page);
            loadConfig(page);
            if (page.dataset.justWatchBound) {
                return;
            }

            page.dataset.justWatchBound = 'true';
            function rememberSettingsEdit(event) {
                settingFields.forEach(function (selector) {
                    if (event.target.matches(selector)) page.settingsEdited.add(selector);
                });
            }
            query(page, '.justWatchSettingsForm').addEventListener('input', rememberSettingsEdit);
            query(page, '.justWatchSettingsForm').addEventListener('change', rememberSettingsEdit);
            page.addEventListener('pagehide', function () { clearSearch(page); Dashboard.hideLoadingMsg(); });
            loadProviderFilters(page);
            query(page, '.justWatchTestSeerr').addEventListener('click', function () { runSettingsCheck(page, this, 'JustWatch/Seerr/Test', 'GET', '.justWatchSeerrFeedback'); });
            query(page, '.justWatchProviderFilters').addEventListener('click', function (event) { handleProviderFilter(page, event); });
            query(page, '.justWatchSettingsForm').addEventListener('submit', function (event) {
                event.preventDefault();
                saveConfig(page);
            });
            query(page, '.justWatchResults').addEventListener('error', function (event) {
                if (event.target.classList.contains('jw-poster')) event.target.hidden = true;
            }, true);
            query(page, '.justWatchResults').addEventListener('click', function (event) { selectCountry(page, event); handleSeerrClick(page, event); });
            query(page, '.justWatchClearButton').addEventListener('click', function () {
                clearSearch(page);
                Dashboard.hideLoadingMsg();
                query(page, '.justWatchTitle').focus();
            });
            query(page, '.justWatchSearchButton').addEventListener('click', function () {
                search(page);
            });
        });
    };
});
