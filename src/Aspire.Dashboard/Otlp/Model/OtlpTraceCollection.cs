// Copyright (c) Lateral Group, 2023. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Turbine.Dashboard.Otlp.Model;

public sealed class OtlpTraceCollection : KeyedCollection<ReadOnlyMemory<byte>, OtlpTrace>
{
    public OtlpTraceCollection() : base(MemoryComparable.Instance, dictionaryCreationThreshold: 0)
    {
    }

    protected override ReadOnlyMemory<byte> GetKeyForItem(OtlpTrace item)
    {
        return item.Key;
    }

    private sealed class MemoryComparable : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public static readonly MemoryComparable Instance = new();

        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            return x.Span.SequenceEqual(y.Span);
        }

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (byte value in obj.Span)
                {
                    hash = hash * 23 + value.GetHashCode();
                }
                return hash;
            }
        }
    }
}