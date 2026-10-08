using System;
using System.Collections.Generic;

namespace PocketDeck.Android;

public sealed record AndroidConnectionSnapshot(long Revision, AndroidConnectionState State, string ReasonCode, string Message, IReadOnlyList<AndroidDeviceView> Devices, AndroidDeviceDetails? SelectedDevice, DateTimeOffset ChangedAt, DateTimeOffset? LastSuccessfulScanAt)
{
	public bool IsReady => State == AndroidConnectionState.Ready;
}
