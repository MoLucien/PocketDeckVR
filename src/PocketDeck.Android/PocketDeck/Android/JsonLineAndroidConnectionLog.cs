using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PocketDeck.Android;

internal sealed class JsonLineAndroidConnectionLog : IAndroidConnectionLogSink, IAsyncDisposable
{
	private readonly Channel<AndroidConnectionLogEntry> _entries;

	private readonly CancellationTokenSource _stopSource = new CancellationTokenSource();

	private readonly Task _writerTask;

	private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private bool _disposed;

	public string? CurrentLogPath { get; }

	public JsonLineAndroidConnectionLog()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string text = Path.Combine(folderPath, "PocketDeck", "logs");
		Directory.CreateDirectory(text);
		CurrentLogPath = Path.Combine(text, $"android-{DateTimeOffset.Now:yyyyMMdd}.jsonl");
		_entries = Channel.CreateBounded<AndroidConnectionLogEntry>(new BoundedChannelOptions(512)
		{
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = false,
			AllowSynchronousContinuations = false
		});
		_writerTask = WriteLoopAsync(_stopSource.Token);
	}

	public bool TryWrite(AndroidConnectionLogEntry entry)
	{
		if (!_disposed)
		{
			return _entries.Writer.TryWrite(entry);
		}
		return false;
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			_entries.Writer.TryComplete();
			try
			{
				await _writerTask.ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				await _stopSource.CancelAsync().ConfigureAwait(continueOnCapturedContext: false);
				_stopSource.Dispose();
			}
		}
	}

	private async Task WriteLoopAsync(CancellationToken cancellationToken)
	{
		_ = 4;
		try
		{
			await using FileStream stream = new FileStream(CurrentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 16384, useAsync: true);
			await using StreamWriter writer = new StreamWriter(stream);
			while (await _entries.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				bool flag = false;
				AndroidConnectionLogEntry item;
				while (_entries.Reader.TryRead(out item))
				{
					if ((object)item != null)
					{
						string text = JsonSerializer.Serialize(item, _serializerOptions);
						await writer.WriteLineAsync(text.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						flag = true;
					}
				}
				if (flag)
				{
					await writer.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
		catch (IOException)
		{
		}
	}
}
