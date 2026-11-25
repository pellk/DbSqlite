using System.Buffers;
using System.Text;

namespace nuel.Sqlite;

internal sealed class Utf8CsvStreamWriter : IDisposable
{
	private readonly Stream _stream;
	private readonly CancellationToken _cancellationToken;
	private byte[] _buffer;
	private int _pos;
	private const int DefaultBufferSize = 4096;

	public Utf8CsvStreamWriter(Stream stream, CancellationToken cancellationToken = default)
	{
		_stream = stream;
		_cancellationToken = cancellationToken;
		_buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
		_pos = 0;
	}

	private async ValueTask EnsureCapacitySlowAsync(int needed)
	{
		await FlushAsync();
		if (_buffer.Length < needed)
		{
			byte[] newBuf = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer.Length * 2, needed));
			ArrayPool<byte>.Shared.Return(_buffer);
			_buffer = newBuf;
		}
	}

	public async ValueTask WriteStringAsync(string str)
	{
		if (string.IsNullOrEmpty(str))
			return;

		int maxBytes = Encoding.UTF8.GetMaxByteCount(str.Length);
		if (_buffer.Length - _pos < maxBytes)
		{
			int actualBytes = Encoding.UTF8.GetByteCount(str);
			if (_buffer.Length - _pos < actualBytes)
			{
				await EnsureCapacitySlowAsync(actualBytes);
			}
		}
		_pos += Encoding.UTF8.GetBytes(str.AsSpan(), _buffer.AsSpan(_pos));
	}

	public async ValueTask FlushAsync()
	{
		if (_pos > 0)
		{
			await _stream.WriteAsync(_buffer.AsMemory(0, _pos), _cancellationToken);
			_pos = 0;
		}
	}

	public async ValueTask FinishAsync()
	{
		await FlushAsync();
		await _stream.FlushAsync(_cancellationToken);
	}

	public void Dispose()
	{
		if (_buffer != null)
		{
			ArrayPool<byte>.Shared.Return(_buffer);
			_buffer = null;
		}
	}
}

