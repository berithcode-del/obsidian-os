using System.Text;

namespace ObsidianInstaller.Core;

/// <summary>
/// Leitor FAT32 somente leitura para imagens de backup de conteúdo (.img).
/// Aceita imagem com MBR (primeira partição FAT32) ou partição FAT32 crua.
/// </summary>
public sealed class Fat32ImageSource : IContentSource, IDisposable
{
    private readonly FileStream _stream;
    private readonly long _partitionOffset;
    private readonly long _partitionLength;
    private readonly int _bytesPerSector;
    private readonly int _sectorsPerCluster;
    private readonly int _reservedSectors;
    private readonly int _fatCount;
    private readonly long _fatStart;
    private readonly long _fatBytes;
    private readonly long _dataStart;
    private readonly uint _rootCluster;
    private readonly long _clusterBytes;
    private readonly Dictionary<uint, uint> _fatCache = [];
    private readonly Dictionary<string, (uint Cluster, long Size)> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private bool _indexed;

    public string Label { get; }
    public string DisplayName => $"{System.IO.Path.GetFileName(ImagePath)} ({Label})";
    private string ImagePath { get; }

    private Fat32ImageSource(FileStream stream, string path, long offset, long length)
    {
        ImagePath = path;
        _stream = stream;
        _partitionOffset = offset;
        _partitionLength = length;

        var bpb = new byte[512];
        ReadAt(offset, bpb, 512);
        _bytesPerSector = BitConverter.ToUInt16(bpb, 11);
        _sectorsPerCluster = bpb[13];
        _reservedSectors = BitConverter.ToUInt16(bpb, 14);
        _fatCount = bpb[16];
        var fatSize16 = BitConverter.ToUInt16(bpb, 22);
        var fatSize32 = BitConverter.ToUInt32(bpb, 36);
        var fatSectors = fatSize16 != 0 ? fatSize16 : fatSize32;
        _rootCluster = BitConverter.ToUInt32(bpb, 44);
        var label = Encoding.ASCII.GetString(bpb, 71, 11).Trim('\0', ' ');
        var fsType = Encoding.ASCII.GetString(bpb, 82, 8).Trim();
        Label = string.IsNullOrWhiteSpace(label) ? fsType : label;

        if (_bytesPerSector < 512 || _bytesPerSector > 4096)
            throw new InvalidDataException("Setor do FAT32 inválido na imagem de conteúdo.");
        if (_sectorsPerCluster is 0 || _reservedSectors == 0 || _fatCount == 0 || fatSectors == 0 || _rootCluster < 2)
            throw new InvalidDataException("Cabeçalho FAT32 inválido na imagem de conteúdo.");
        if (!fsType.StartsWith("FAT32", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"A imagem de conteúdo não é FAT32 (encontrado: {fsType}).");

        _fatStart = offset + (_reservedSectors * (long)_bytesPerSector);
        _fatBytes = fatSectors * (long)_bytesPerSector;
        _dataStart = offset + ((_reservedSectors + (_fatCount * (long)fatSectors)) * _bytesPerSector);
        _clusterBytes = (long)_sectorsPerCluster * _bytesPerSector;
    }

    public static Fat32ImageSource Open(string imagePath)
    {
        var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: false);
        try
        {
            var head = new byte[512];
            stream.ReadExactly(head, 0, 512);
            var isMbr = head[510] == 0x55 && head[511] == 0xAA;
            if (isMbr)
            {
                var parts = MbrTable.Parse(head);
                var fat = parts.FirstOrDefault(p => p.Type is 0x0B or 0x0C && p.StartSector > 0);
                if (fat.SectorCount == 0)
                    throw new InvalidDataException("A imagem de conteúdo não tem partição FAT32.");
                return new Fat32ImageSource(stream, imagePath, fat.StartByte, fat.SectorCount * 512L);
            }
            // Imagem de partição crua.
            return new Fat32ImageSource(stream, imagePath, 0, stream.Length);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private void ReadAt(long absoluteOffset, byte[] buffer, int count)
    {
        var local = absoluteOffset - _partitionOffset;
        if (local < 0 || local + count > _partitionLength)
            throw new InvalidDataException("Leitura fora da partição FAT32.");
        _stream.Position = absoluteOffset;
        _stream.ReadExactly(buffer, 0, count);
    }

    private uint NextCluster(uint cluster)
    {
        if (_fatCache.TryGetValue(cluster, out var cached))
            return cached;
        var entry = new byte[4];
        ReadAt(_fatStart + cluster * 4L, entry, 4);
        var value = BitConverter.ToUInt32(entry, 0) & 0x0FFFFFFF;
        _fatCache[cluster] = value;
        return value;
    }

    private byte[] ReadCluster(uint cluster)
    {
        var buffer = new byte[_clusterBytes];
        ReadAt(_dataStart + (cluster - 2) * _clusterBytes, buffer, (int)_clusterBytes);
        return buffer;
    }

    private List<(string Name, bool IsDirectory, uint Cluster, long Size)> ReadDirectory(uint startCluster)
    {
        var entries = new List<(string, bool, uint, long)>();
        var lfnParts = new List<(int Order, string Text)>();
        var cluster = startCluster;
        var guard = 0;
        while (cluster is >= 2 and < 0x0FFFFFF8 && guard++ < 65536)
        {
            var data = ReadCluster(cluster);
            for (var offset = 0; offset + 32 <= data.Length; offset += 32)
            {
                if (data[offset] == 0x00)
                    return entries;
                if (data[offset] == 0xE5)
                {
                    lfnParts.Clear();
                    continue;
                }
                var attr = data[offset + 11];
                if (attr == 0x0F)
                {
                    var order = data[offset] & 0x1F;
                    var text = DecodeLfnPart(data, offset);
                    lfnParts.Add((order, text));
                    continue;
                }
                if ((attr & 0x08) != 0) // rótulo de volume
                    continue;

                var name = lfnParts.Count > 0
                    ? string.Concat(lfnParts.OrderBy(p => p.Order).Select(p => p.Text))
                    : DecodeShortName(data, offset);
                lfnParts.Clear();

                if (name is "." or "..")
                    continue;

                var clusterHigh = BitConverter.ToUInt16(data, offset + 20);
                var clusterLow = BitConverter.ToUInt16(data, offset + 26);
                var firstCluster = ((uint)clusterHigh << 16) | clusterLow;
                var size = BitConverter.ToUInt32(data, offset + 28);
                entries.Add((name, (attr & 0x10) != 0, firstCluster, size));
            }
            cluster = NextCluster(cluster);
        }
        return entries;
    }

    private static string DecodeLfnPart(byte[] data, int offset)
    {
        var builder = new StringBuilder(13);
        void Add(int from, int to)
        {
            for (var i = from; i < to; i += 2)
            {
                var ch = (char)BitConverter.ToUInt16(data, offset + i);
                if (ch == '\0' || ch == '\uffff')
                    continue;
                builder.Append(ch);
            }
        }
        Add(1, 11);
        Add(14, 26);
        Add(28, 32);
        return builder.ToString();
    }

    private static string DecodeShortName(byte[] data, int offset)
    {
        var name = Encoding.ASCII.GetString(data, offset, 8).TrimEnd();
        var extension = Encoding.ASCII.GetString(data, offset + 8, 3).TrimEnd();
        return extension.Length > 0 ? $"{name}.{extension}" : name;
    }

    private void BuildIndex()
    {
        if (_indexed)
            return;
        var pending = new Queue<(string Prefix, uint Cluster)>();
        pending.Enqueue((string.Empty, _rootCluster));
        var guard = 0;
        while (pending.Count > 0 && guard++ < 100000)
        {
            var (prefix, cluster) = pending.Dequeue();
            foreach (var entry in ReadDirectory(cluster))
            {
                var path = prefix.Length == 0 ? entry.Name : $"{prefix}/{entry.Name}";
                if (entry.IsDirectory)
                {
                    _directories.Add(path);
                    if (entry.Cluster >= 2)
                        pending.Enqueue((path, entry.Cluster));
                }
                else
                {
                    _index[path] = (entry.Cluster, entry.Size);
                }
            }
        }
        _indexed = true;
    }

    public IReadOnlyList<ContentItem> ScanContent()
    {
        BuildIndex();
        var items = new List<ContentItem>();
        foreach (var kind in ContentKinds.All)
        {
            var prefix = kind + "/";
            var files = _index.Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(System.IO.Path.GetFileNameWithoutExtension(kv.Key), kind, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (files.Length > 0)
                items.Add(new ContentItem(kind, files.Length, files.Sum(f => f.Value.Size)));
            else if (_directories.Contains(kind))
                items.Add(new ContentItem(kind, 0, 0));
        }
        return items;
    }

    public IReadOnlyList<ContentFileInfo> EnumerateFiles(string kind)
    {
        BuildIndex();
        var prefix = kind + "/";
        return _index
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => new ContentFileInfo(kind, kv.Key, kv.Value.Size))
            .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Stream OpenRead(ContentFileInfo file)
    {
        BuildIndex();
        if (!_index.TryGetValue(file.RelativePath, out var entry))
            throw new FileNotFoundException($"Arquivo não encontrado na imagem: {file.RelativePath}");
        return new Fat32FileStream(this, entry.Cluster, entry.Size);
    }

    public void Dispose() => _stream.Dispose();

    private sealed class Fat32FileStream(Fat32ImageSource source, uint startCluster, long length) : Stream
    {
        private long _position;
        private uint _cluster = startCluster;
        private long _clusterOffset;
        private byte[]? _buffer;
        private uint _bufferCluster = uint.MaxValue;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= length || count <= 0)
                return 0;
            if (_buffer is null || _bufferCluster != _cluster)
            {
                _buffer = source.ReadCluster(_cluster);
                _bufferCluster = _cluster;
            }
            var available = (int)(source._clusterBytes - _clusterOffset);
            var toCopy = (int)Math.Min(Math.Min(count, length - _position), available);
            if (toCopy <= 0)
                return 0;
            Array.Copy(_buffer, _clusterOffset, buffer, offset, toCopy);
            _clusterOffset += toCopy;
            _position += toCopy;
            if (_clusterOffset >= source._clusterBytes)
            {
                var next = source.NextCluster(_cluster);
                if (next is < 2 or >= 0x0FFFFFF8)
                    _position = length;
                else
                {
                    _cluster = next;
                    _clusterOffset = 0;
                }
            }
            return toCopy;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private int ReadClusterBytes(uint cluster) => (int)_clusterBytes;
}
