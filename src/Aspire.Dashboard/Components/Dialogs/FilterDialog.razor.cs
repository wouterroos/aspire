// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Turbine.Dashboard.Model;
using Turbine.Dashboard.Model.Otlp;
using Turbine.Dashboard.Otlp.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Turbine.Dashboard.Components.Dialogs;

public partial class FilterDialog
{
    private List<SelectViewModel<FilterCondition>> _filterConditions = null!;

    private SelectViewModel<FilterCondition> CreateFilterSelectViewModel(FilterCondition condition) =>
        new SelectViewModel<FilterCondition> { Id = condition, Name = LogFilter.ConditionToString(condition, LogsLoc) };

    [CascadingParameter]
    public FluentDialog? Dialog { get; set; }

    [Parameter]
    public FilterDialogViewModel Content { get; set; } = default!;

    [Inject]
    public required TelemetryRepository TelemetryRepository { get; init; }

    private LogDialogFormModel _formModel = default!;
    private List<SelectViewModel<string>> _parameters = default!;

    public EditContext EditContext { get; private set; } = default!;

    protected override void OnInitialized()
    {
        _filterConditions =
        [
            CreateFilterSelectViewModel(FilterCondition.Equals),
            CreateFilterSelectViewModel(FilterCondition.Contains),
            CreateFilterSelectViewModel(FilterCondition.NotEqual),
            CreateFilterSelectViewModel(FilterCondition.NotContains)
        ];

        _formModel = new LogDialogFormModel();
        EditContext = new EditContext(_formModel);
    }

    protected override void OnParametersSet()
    {
        List<string>? names = new List<string> { LogFilter.KnownMessageField, LogFilter.KnownCategoryField, LogFilter.KnownApplicationField, LogFilter.KnownTraceIdField, LogFilter.KnownSpanIdField, LogFilter.KnownOriginalFormatField };
        List<SelectViewModel<string>>? knownFields = names.Select(p => new SelectViewModel<string> { Id = p, Name = LogFilter.ResolveFieldName(p) }).ToList();
        List<SelectViewModel<string>>? customFields = Content.LogPropertyKeys.Select(p => new SelectViewModel<string> { Id = p, Name = LogFilter.ResolveFieldName(p) }).ToList();

        _parameters =
        [
            .. knownFields,
            new SelectViewModel<string> { Id = null, Name = "-" },
            .. customFields
        ];

        if (Content.Filter is { } logFilter)
        {
            _formModel.Parameter = _parameters.SingleOrDefault(c => c.Id == logFilter.Field);
            _formModel.Condition = _filterConditions.Single(c => c.Id == logFilter.Condition);
            _formModel.Value = logFilter.Value;
        }
        else
        {
            _formModel.Parameter = _parameters.SingleOrDefault(c => c.Id == LogFilter.KnownMessageField);
            _formModel.Condition = _filterConditions.Single(c => c.Id == FilterCondition.Contains);
            _formModel.Value = "";
        }
    }

    private void Cancel()
    {
        Dialog!.CancelAsync();
    }

    private void Delete()
    {
        Dialog!.CloseAsync(DialogResult.Ok(new FilterDialogResult() { Filter = Content.Filter, Delete = true }));
    }

    private void Apply()
    {
        if (Content.Filter is { } logFilter)
        {
            logFilter.Field = _formModel.Parameter!.Id!;
            logFilter.Condition = _formModel.Condition!.Id;
            logFilter.Value = _formModel.Value!;

            Dialog!.CloseAsync(DialogResult.Ok(new FilterDialogResult() { Filter = logFilter, Delete = false }));
        }
        else
        {
            LogFilter? filter = new LogFilter
            {
                Field = _formModel.Parameter!.Id!,
                Condition = _formModel.Condition!.Id,
                Value = _formModel.Value!
            };

            Dialog!.CloseAsync(DialogResult.Ok(new FilterDialogResult() { Filter = filter, Add = true }));
        }
    }
}