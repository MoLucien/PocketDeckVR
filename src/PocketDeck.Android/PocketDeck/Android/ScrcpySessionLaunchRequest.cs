using System;
using System.Collections.Generic;
using PocketDeck.Contracts;

namespace PocketDeck.Android;

internal sealed record ScrcpySessionLaunchRequest(ResolvedAndroidDevice Device, ScrcpySessionKind Kind, Func<int, IReadOnlyList<string>> BuildServerArguments, OperationDeadline Deadline);
