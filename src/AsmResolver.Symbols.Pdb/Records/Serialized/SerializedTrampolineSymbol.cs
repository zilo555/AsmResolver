using AsmResolver.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsmResolver.Symbols.Pdb.Records.Serialized;

/// <summary>
/// Represents a lazily initialized implementation of <see cref="TrampolineSymbol"/> that is read from a PDB image.
/// </summary>

public class SerializedTrampolineSymbol : TrampolineSymbol
{
    /// <summary>
    /// Reads a trampoline symbol from the provided input stream.
    /// </summary>
    /// <param name="reader">The input stream to read from.</param>
    public SerializedTrampolineSymbol(BinaryStreamReader reader)
    {
        TrampolineSymbolKind kind = (TrampolineSymbolKind)reader.ReadUInt16();
        ushort thunkSize = reader.ReadUInt16();
        ushort thunkOffset = reader.ReadUInt16();
        ushort targetOffset = reader.ReadUInt16();
        ushort thunkSegment = reader.ReadUInt16();
        ushort targetSegment = reader.ReadUInt16();

        Kind = kind;
        ThunkSize = thunkSize;
        ThunkOffset = thunkOffset;
        TargetOffset = targetOffset;
        ThunkSegmentIndex = thunkSegment;
        TargetSegmentIndex = targetSegment;
    }
}
