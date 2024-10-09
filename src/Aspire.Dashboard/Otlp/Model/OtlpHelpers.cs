// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbine.Dashboard.Configuration;
using Turbine.Dashboard.Otlp.Storage;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Turbine.Dashboard.Otlp.Model;

public static class OtlpHelpers
{
    // Reduce size of JSON data by not indenting. Dashboard UI supports formatting JSON values when they're displayed.
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = false
    };

    public static ApplicationKey GetApplicationKey(this Resource resource)
    {
        string? serviceName = null;
        string? serviceInstanceId = null;
        string? processExecutableName = null;

        for (int i = 0; i < resource.Attributes.Count; i++)
        {
            KeyValue? attribute = resource.Attributes[i];
            if (attribute.Key == OtlpApplication.SERVICE_INSTANCE_ID)
            {
                serviceInstanceId = attribute.Value.GetString();
            }
            if (attribute.Key == OtlpApplication.SERVICE_NAME)
            {
                serviceName = attribute.Value.GetString();
            }
            if (attribute.Key == OtlpApplication.PROCESS_EXECUTABLE_NAME)
            {
                processExecutableName = attribute.Value.GetString();
            }
        }

        // Fallback to unknown_service if service name isn't specified.
        // https://github.com/open-telemetry/opentelemetry-specification/issues/3210
        if (string.IsNullOrEmpty(serviceName))
        {
            serviceName = "unknown_service";
            if (!string.IsNullOrEmpty(processExecutableName))
            {
                serviceName += ":" + processExecutableName;
            }
        }

        // service.instance.id is recommended but not required.
        return new ApplicationKey(serviceName, serviceInstanceId ?? serviceName);
    }

    public static string ToShortenedId(string id) => TruncateString(id, maxLength: 7);

    public static string ToHexString(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        // This produces lowercase hex string from the bytes. It's used instead of Convert.ToHexString()
        // because we want to display lowercase hex string in the UI for values such as traceid and spanid.
        return string.Create(bytes.Length * 2, bytes, static (chars, bytes) =>
        {
            ReadOnlySpan<byte> data = bytes.Span;
            for (int pos = 0; pos < data.Length; pos++)
            {
                ToCharsBuffer(data[pos], chars, pos * 2);
            }
        });
    }

    public static string TruncateString(string value, int maxLength)
    {
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    public static string ToHexString(this ByteString bytes)
    {
        return ToHexString(bytes.Memory);
    }

    public static string GetString(this AnyValue value) =>
        value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue ? "true" : "false",
            AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToHexString(),
            AnyValue.ValueOneofCase.ArrayValue => ConvertAnyValue(value)!.ToJsonString(s_jsonSerializerOptions),
            AnyValue.ValueOneofCase.KvlistValue => ConvertAnyValue(value)!.ToJsonString(s_jsonSerializerOptions),
            AnyValue.ValueOneofCase.None => string.Empty,
            _ => value.ToString(),
        };

    private static JsonNode? ConvertAnyValue(AnyValue value)
    {
        // Recursively convert AnyValue types to JsonNode types to produce more idiomatic JSON.
        // Recursing over incoming values is safe because Protobuf serializer imposes a safe limit on recursive messages.
        return value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => JsonValue.Create(value.StringValue),
            AnyValue.ValueOneofCase.IntValue => JsonValue.Create(value.IntValue),
            AnyValue.ValueOneofCase.DoubleValue => JsonValue.Create(value.DoubleValue),
            AnyValue.ValueOneofCase.BoolValue => JsonValue.Create(value.BoolValue),
            AnyValue.ValueOneofCase.BytesValue => JsonValue.Create(value.BytesValue.ToHexString()),
            AnyValue.ValueOneofCase.ArrayValue => ConvertArray(value.ArrayValue),
            AnyValue.ValueOneofCase.KvlistValue => ConvertKeyValues(value.KvlistValue),
            AnyValue.ValueOneofCase.None => null,
            _ => throw new InvalidOperationException($"Unexpected AnyValue type: {value.ValueCase}"),
        };

        static JsonArray ConvertArray(ArrayValue value)
        {
            JsonArray? a = new JsonArray();
            foreach (AnyValue? item in value.Values)
            {
                a.Add(ConvertAnyValue(item));
            }
            return a;
        }

        static JsonObject ConvertKeyValues(KeyValueList value)
        {
            JsonObject? o = new JsonObject();
            foreach (KeyValue? item in value.Values)
            {
                o[item.Key] = ConvertAnyValue(item.Value);
            }
            return o;
        }
    }

    // From https://github.com/dotnet/runtime/blob/963954a11c1beeea4ad63002084a213d8d742864/src/libraries/Common/src/System/HexConverter.cs#L81-L89
    // Modified slightly to always produce lowercase output.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToCharsBuffer(byte value, Span<char> buffer, int startingIndex = 0)
    {
        uint difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        uint packedResult = (((uint)-(int)difference & 0x7070U) >> 4) + difference + 0xB9B9U | 0x2020U;

        buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
        buffer[startingIndex] = (char)(packedResult >> 8);
    }

    public static DateTime UnixNanoSecondsToDateTime(ulong unixTimeNanoseconds)
    {
        long ticks = NanosecondsToTicks(unixTimeNanoseconds);

        return DateTime.UnixEpoch.AddTicks(ticks);
    }

    private static long NanosecondsToTicks(ulong nanoseconds)
    {
        return (long)(nanoseconds / TimeSpan.NanosecondsPerTick);
    }

    public static KeyValuePair<string, string>[] ToKeyValuePairs(this RepeatedField<KeyValue> attributes, TelemetryLimitOptions options)
    {
        if (attributes.Count == 0)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        KeyValuePair<string, string>[]? values = new KeyValuePair<string, string>[Math.Min(attributes.Count, options.MaxAttributeCount)];
        CopyKeyValues(attributes, values, index: 0, options);

        return values;
    }

    public static KeyValuePair<string, string>[] ToKeyValuePairs(this RepeatedField<KeyValue> attributes, TelemetryLimitOptions options, Func<KeyValue, bool> filter)
    {
        if (attributes.Count == 0)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        int readLimit = Math.Min(attributes.Count, options.MaxAttributeCount);
        List<KeyValuePair<string, string>>? values = new List<KeyValuePair<string, string>>(readLimit);
        for (int i = 0; i < attributes.Count; i++)
        {
            KeyValue? attribute = attributes[i];

            if (!filter(attribute))
            {
                continue;
            }

            string? value = TruncateString(attribute.Value.GetString(), options.MaxAttributeLength);

            values.Add(new KeyValuePair<string, string>(attribute.Key, value));

            if (values.Count >= readLimit)
            {
                break;
            }
        }

        return values.ToArray();
    }

    public static void CopyKeyValuePairs(RepeatedField<KeyValue> attributes, KeyValuePair<string, string>[] parentAttributes, TelemetryLimitOptions options, out int copyCount, [NotNull] ref KeyValuePair<string, string>[]? copiedAttributes)
    {
        copyCount = Math.Min(parentAttributes.Length + attributes.Count, options.MaxAttributeCount);

        if (copiedAttributes is null || copiedAttributes.Length < copyCount)
        {
            copiedAttributes = new KeyValuePair<string, string>[copyCount];
        }
        else
        {
            Array.Clear(copiedAttributes);
        }

        parentAttributes.AsSpan().CopyTo(copiedAttributes);

        CopyKeyValues(attributes, copiedAttributes, parentAttributes.Length, options);
    }

    private static void CopyKeyValues(RepeatedField<KeyValue> attributes, KeyValuePair<string, string>[] copiedAttributes, int index, TelemetryLimitOptions options)
    {
        int copyCount = Math.Min(attributes.Count + index, options.MaxAttributeCount);

        for (int i = 0; i < copyCount - index; i++)
        {
            KeyValue? attribute = attributes[i];

            string? value = TruncateString(attribute.Value.GetString(), options.MaxAttributeLength);

            copiedAttributes[i + index] = new KeyValuePair<string, string>(attribute.Key, value);
        }
    }

    public static string? GetValue(this KeyValuePair<string, string>[] values, string name)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Key == name)
            {
                return values[i].Value;
            }
        }
        return null;
    }

    public static string? GetPeerAddress(this KeyValuePair<string, string>[] values)
    {
        string? address = GetValue(values, OtlpSpan.PeerServiceAttributeKey);
        if (address != null)
        {
            return address;
        }

        // OTEL HTTP 1.7.0 doesn't return peer.service. Fallback to server.address and server.port.
        if (GetValue(values, OtlpSpan.ServerAddressAttributeKey) is { } server)
        {
            if (GetValue(values, OtlpSpan.ServerPortAttributeKey) is { } serverPort)
            {
                server += ":" + serverPort;
            }
            return server;
        }

        // Fallback to older names of net.peer.name and net.peer.port.
        if (GetValue(values, OtlpSpan.NetPeerNameAttributeKey) is { } peer)
        {
            if (GetValue(values, OtlpSpan.NetPeerPortAttributeKey) is { } peerPort)
            {
                peer += ":" + peerPort;
            }
            return peer;
        }

        return null;
    }

    public static bool HasKey(this KeyValuePair<string, string>[] values, string name)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Key == name)
            {
                return true;
            }
        }
        return false;
    }

    public static string ConcatProperties(this KeyValuePair<string, string>[] properties)
    {
        StringBuilder sb = new();
        bool first = true;
        foreach (KeyValuePair<string, string> kv in properties)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;
            sb.Append(CultureInfo.InvariantCulture, $"{kv.Key}: ");
            sb.Append(string.IsNullOrEmpty(kv.Value) ? "\'\'" : kv.Value);
        }
        return sb.ToString();
    }

    public static PagedResult<T> GetItems<T>(IEnumerable<T> results, int startIndex, int? count)
    {
        return GetItems<T, T>(results, startIndex, count, null);
    }

    public static PagedResult<TResult> GetItems<TSource, TResult>(IEnumerable<TSource> results, int startIndex, int? count, Func<TSource, TResult>? select)
    {
        IEnumerable<TSource>? query = results.Skip(startIndex);
        if (count != null)
        {
            query = query.Take(count.Value);
        }
        List<TResult> items;
        if (select != null)
        {
            items = query.Select(select).ToList();
        }
        else
        {
            items = query.Cast<TResult>().ToList();
        }
        int totalItemCount = results.Count();

        return new PagedResult<TResult>
        {
            Items = items,
            TotalItemCount = totalItemCount
        };
    }
}