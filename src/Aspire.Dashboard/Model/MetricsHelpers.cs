// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Dashboard.Utils;
using Turbine.Dashboard.Otlp.Model;
using Turbine.Dashboard.Resources;
using Turbine.Dashboard.Utils;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Model;

public static class MetricsHelpers
{
    public static async Task<bool> WaitForSpanToBeAvailableAsync(
        string traceId,
        string spanId,
        Func<string, string, OtlpSpan?> getSpan,
        IDialogService dialogService,
        Func<Func<Task>, Task> dispatcher,
        IStringLocalizer<Dialogs> loc,
        CancellationToken cancellationToken)
    {
        OtlpSpan? span = getSpan(traceId, spanId);

        // Exemplar span isn't loaded yet. Display a dialog until the data is ready or the user cancels the dialog.
        if (span == null)
        {
            using CancellationTokenSource? cts = new CancellationTokenSource();
            using CancellationTokenRegistration registration = cancellationToken.Register(cts.Cancel);

            IDialogReference? reference = await dialogService.ShowMessageBoxAsync(new DialogParameters<MessageBoxContent>()
            {
                Content = new MessageBoxContent
                {
                    Intent = MessageBoxIntent.Info,
                    Icon = new Icons.Filled.Size24.Info(),
                    IconColor = Color.Info,
                    Message = string.Format(CultureInfo.InvariantCulture, loc[nameof(Dialogs.OpenTraceDialogMessage)], OtlpHelpers.ToShortenedId(traceId)),
                },
                DialogType = DialogType.MessageBox,
                PrimaryAction = string.Empty,
                SecondaryAction = loc[nameof(Dialogs.OpenTraceDialogCancelButtonText)]
            }).ConfigureAwait(false);

            // Task that polls for the span to be available.
            Task? waitForTraceTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    span = getSpan(traceId, spanId);
                    if (span != null)
                    {
                        await dispatcher(async () =>
                        {
                            await reference.CloseAsync(DialogResult.Ok<bool>(true)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5), cts.Token).ConfigureAwait(false);
                    }
                }
            }, cts.Token);

            DialogResult? result = await reference.Result.ConfigureAwait(false);
            cts.Cancel();

            await TaskHelpers.WaitIgnoreCancelAsync(waitForTraceTask).ConfigureAwait(false);

            if (result.Cancelled)
            {
                // Dialog was canceled before span was ready. Exit without navigating.
                return false;
            }
        }

        return true;
    }
}