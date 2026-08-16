(function () {
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
            Dashboard.processPluginConfigurationUpdateResult();
        });
    }

    function renderResults(page, results) {
        const container = query(page, '.justWatchResults');
        if (!results || !results.length) {
            container.innerHTML = '<p>No JustWatch matches found.</p>';
            return;
        }

        container.innerHTML = results.map(function (item) {
            const providers = item.Providers && item.Providers.length ? item.Providers.join(', ') : 'No providers returned';
            const year = item.Year ? ' (' + item.Year + ')' : '';
            const link = item.JustWatchUrl ? '<a is="emby-linkbutton" class="button-link" target="_blank" href="' + escapeHtml(item.JustWatchUrl) + '">Open JustWatch</a>' : '';
            return '<div class="paperList" style="padding:1em;margin-bottom:.75em;">'
                + '<h3 style="margin-top:0;">' + escapeHtml(item.Title) + escapeHtml(year) + '</h3>'
                + '<p>' + escapeHtml(providers) + '</p>'
                + link
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

        Dashboard.showLoadingMsg();
        ApiClient.ajax({ type: 'GET', url: url, dataType: 'json' }).then(function (results) {
            renderResults(page, results);
            Dashboard.hideLoadingMsg();
        }, function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('JustWatch lookup failed.');
        });
    }

    document.querySelector('.justWatchConfigurationPage')
        .addEventListener('pageshow', function () {
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
})();
