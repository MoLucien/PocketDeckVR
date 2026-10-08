using System;
using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Session;

public interface IPhoneControlService : IAsyncDisposable
{
	PhoneControlSnapshot Snapshot { get; }

	PhoneControlSnapshot DiagnosticSnapshot { get; }

	event EventHandler<PhoneControlChangedEventArgs>? StateChanged;

	ValueTask StartAsync(string? deviceKey, CancellationToken cancellationToken);

	ValueTask SendAsync(PhoneInputCommandKind kind, float normalizedX, float normalizedY, int screenWidth, int screenHeight, float scrollDelta, CancellationToken cancellationToken, int unlockDigit = -1);

	void QueueFromSteamVr(PhoneInputCommandKind kind, float normalizedX, float normalizedY, int screenWidth, int screenHeight, float scrollDelta = 0f, int unlockDigit = -1);

	ValueTask StopAsync(CancellationToken cancellationToken);
}
