define([], function () {
    'use strict';
    function init(page, pluginId) {
        var form = page.querySelector('form');
        var fields = form.elements;
        var ready = false;
        var passwordSaved = false;
        function showSavedPassword(saved) {
            fields.password.value = '';
            fields.password.placeholder = saved ? '••••••••' : '';
            form.querySelector('.password-status').textContent = saved ? 'Password saved' : '';
        }
        var save = form.querySelector('button[type=submit]');
        function load() {
            ready = false;
            save.disabled = true;
            Promise.all([ApiClient.getPluginConfiguration(pluginId), ApiClient.getUsers()]).then(function (values) {
                var config = values[0];
                fields.user.textContent = '';
                fields.user.add(new Option('Choose a server user', ''));
                values[1].forEach(function (user) { fields.user.add(new Option(user.Name, user.Id)); });
                fields.user.value = config.PlaybackUserId || '';
                fields.username.value = config.Username || '';
                showSavedPassword(!!config.Password);
                fields.enabled.checked = !!config.Enabled;
                ready = true;
                save.disabled = false;
            }).catch(function () { Dashboard.alert('Could not load Next Episode Sync settings.'); });
        }
        page.addEventListener('viewshow', load);
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (!ready || save.disabled) return;
            save.disabled = true;
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                config.Enabled = fields.enabled.checked;
                config.PlaybackUserId = fields.user.value;
                config.Username = fields.username.value.trim();
                if (fields.password.value) config.Password = fields.password.value;
                if (config.Enabled && (!config.Password || !config.Username || config.Username.indexOf('@') >= 0)) {
                    throw new Error('Enter your Next Episode profile username and password to enable sync.');
                }
                passwordSaved = !!config.Password;
                return ApiClient.updatePluginConfiguration(pluginId, config);
            }).then(function (result) {
                showSavedPassword(passwordSaved);
                Dashboard.processPluginConfigurationUpdateResult(result);
            }).catch(function (error) {
                Dashboard.alert(error instanceof Error ? error.message : 'Could not save settings.');
            }).finally(function () { save.disabled = false; });
        });
        load();
    }
    return function (view) {
        init(view, 'e1b3f411-61b9-4fd0-947b-36936228005d');
    };
});
