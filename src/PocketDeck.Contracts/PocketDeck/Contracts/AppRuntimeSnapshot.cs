using System;

namespace PocketDeck.Contracts;

public sealed record AppRuntimeSnapshot(AppLifecycleState State, StateReason Reason, DateTimeOffset ChangedAt);
