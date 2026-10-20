// Read the PE CodeView RSDS identity needed to request a matching public PDB.
// Compatible with Windows PowerShell's Add-Type compiler and .NET Framework.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DrcLink
{
    public sealed class SymbolIdentity
    {
        public string PdbName { get; private set; }
        public string Guid { get; private set; }
        public uint Age { get; private set; }
        public string SymbolServerKey { get; private set; }

        static uint U32(byte[] b, long p)
        {
            Check(b, p, 4);
            return BitConverter.ToUInt32(b, (int)p);
        }
        static ushort U16(byte[] b, long p)
        {
            Check(b, p, 2);
            return BitConverter.ToUInt16(b, (int)p);
        }
        static void Check(byte[] b, long p, long n)
        {
            if (p < 0 || n < 0 || p > b.LongLength || n > b.LongLength - p)
                throw new InvalidDataException("Truncated PE/debug data.");
        }
        static long Offset(byte[] b, uint rva, long sections, ushort count)
        {
            for (int i = 0; i < count; i++)
            {
                long s = sections + i * 40L;
                uint start = U32(b, s + 12), size = U32(b, s + 16);
                if (rva >= start && (ulong)rva - start < size)
                    return U32(b, s + 20) + (long)rva - start;
            }
            throw new InvalidDataException("Debug RVA is outside file-backed sections.");
        }
        public static SymbolIdentity[] Read(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (U16(b, 0) != 0x5a4d) throw new InvalidDataException("Missing MZ signature.");
            long pe = U32(b, 0x3c);
            if (U32(b, pe) != 0x4550) throw new InvalidDataException("Missing PE signature.");
            ushort count = U16(b, pe + 6), optSize = U16(b, pe + 20);
            long opt = pe + 24, end = opt + optSize;
            Check(b, opt, optSize);
            ushort magic = U16(b, opt);
            int dd = magic == 0x20b ? 112 : magic == 0x10b ? 96 : -1;
            if (dd < 0) throw new InvalidDataException("Unknown PE optional header.");
            if (opt + dd + 56 > end || U32(b, opt + dd - 4) <= 6)
                return new SymbolIdentity[0];
            uint rva = U32(b, opt + dd + 48), size = U32(b, opt + dd + 52);
            if (rva == 0 || size == 0) return new SymbolIdentity[0];
            if (size % 28 != 0) throw new InvalidDataException("Malformed debug directory.");
            Check(b, end, count * 40L);
            long debug = Offset(b, rva, end, count);
            Check(b, debug, size);
            var result = new List<SymbolIdentity>();
            for (long p = debug; p < debug + size; p += 28)
            {
                if (U32(b, p + 12) != 2) continue; // IMAGE_DEBUG_TYPE_CODEVIEW
                uint length = U32(b, p + 16), raw = U32(b, p + 24);
                Check(b, raw, length);
                if (length < 24 || U32(b, raw) != 0x53445352) continue; // RSDS
                byte[] id = new byte[16];
                Array.Copy(b, (long)raw + 4, id, 0, 16);
                string guid = new System.Guid(id).ToString("N").ToUpperInvariant();
                uint age = U32(b, (long)raw + 20);
                int n = 24;
                while (n < length && b[(long)raw + n] != 0) n++;
                if (n == length) throw new InvalidDataException("Unterminated PDB name.");
                string name = Encoding.UTF8.GetString(b, (int)raw + 24, n - 24).Replace('\\', '/');
                name = name.Substring(name.LastIndexOf('/') + 1);
                result.Add(new SymbolIdentity {
                    PdbName = name, Guid = guid, Age = age,
                    SymbolServerKey = guid + age.ToString("X")
                });
            }
            return result.ToArray();
        }
    }
}
