// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Turbine.Dashboard.Components.Dialogs;

public partial class SettingsDialog : IDialogContentComponent, IAsyncDisposable
{
    private string _currentSetting = ThemeManager.ThemeSettingSystem;

    private IJSObjectReference? _jsModule;
    private IDisposable? _themeChangedSubscription;

    [Inject]
    public required IJSRuntime JS { get; init; }

    [Inject]
    public required ThemeManager ThemeManager { get; init; }

    protected override void OnInitialized()
    {
        // Handle value being changed in a different browser window.
        _themeChangedSubscription = ThemeManager.OnThemeChanged(async () =>
        {
            string? newValue = ThemeManager.Theme!;
            if (_currentSetting != newValue)
            {
                _currentSetting = newValue;
                await InvokeAsync(StateHasChanged);
            }
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/js/app-theme.js");
            _currentSetting = await _jsModule.InvokeAsync<string>("getThemeCookieValue");
            StateHasChanged();
        }
    }

    private async Task SettingChangedAsync(string newValue)
    {
        // The theme isn't changed here. Instead, the MainLayout subscribes to the change event
        // and applies the new theme to the browser window.
        _currentSetting = newValue;
        await ThemeManager.RaiseThemeChangedAsync(newValue);
    }

    public async ValueTask DisposeAsync()
    {
        _themeChangedSubscription?.Dispose();
        await JSInteropHelpers.SafeDisposeAsync(_jsModule);
    }
}