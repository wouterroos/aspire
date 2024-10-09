// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace Turbine.Dashboard.Otlp.Model;

public sealed class GeneratedColor
{
    public required string Hex { get; init; }
    public required int Red { get; init; }
    public required int Green { get; init; }
    public required int Blue { get; init; }
}

public class ColorGenerator
{
    private static readonly string[] s_colorsHex =
    [
        "#17B8BE", "#F8DCA1", "#B7885E", "#FFCB99", "#F89570",
        "#829AE3", "#E79FD5", "#1E96BE", "#89DAC1", "#B3AD9E",
        "#12939A", "#DDB27C", "#88572C", "#FF9833", "#EF5D28",
        "#162A65", "#DA70BF", "#125C77", "#4DC19C", "#776E57"
    ];

    public static readonly ColorGenerator Instance = new ColorGenerator();

    private readonly List<GeneratedColor> _colors;
    private readonly ConcurrentDictionary<string, Lazy<int>> _colorIndexByKey;
    private int _currentIndex;

    private ColorGenerator()
    {
        _colors = new List<GeneratedColor>();
        _colorIndexByKey = new ConcurrentDictionary<string, Lazy<int>>(StringComparer.OrdinalIgnoreCase);
        _currentIndex = 0;

        foreach (string? hex in s_colorsHex)
        {
            (int Red, int Green, int Blue) rgb = GetHexRgb(hex);
            _colors.Add(new GeneratedColor
            {
                Hex = hex,
                Red = rgb.Red,
                Green = rgb.Green,
                Blue = rgb.Blue
            });
        }
    }

    private static (int Red, int Green, int Blue) GetHexRgb(string s)
    {
        if (s.Length != 7)
        {
            return (0, 0, 0);
        }

        int r = int.Parse(s.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int g = int.Parse(s.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int b = int.Parse(s.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return (r, g, b);
    }

    private int GetColorIndex(string key)
    {
        return _colorIndexByKey.GetOrAdd(key, k =>
        {
            // GetOrAdd is run outside of the lock.
            // Use lazy to ensure that the index is only calculated once for an app.
            return new Lazy<int>(() =>
            {
                int i = _currentIndex;
                _currentIndex = ++_currentIndex % _colors.Count;
                return i;
            });
        }).Value;
    }

    public string GetColorHexByKey(string key)
    {
        int i = GetColorIndex(key);
        return _colors[i].Hex;
    }

    public void Clear()
    {
        _colorIndexByKey.Clear();
        _currentIndex = 0;
    }
}