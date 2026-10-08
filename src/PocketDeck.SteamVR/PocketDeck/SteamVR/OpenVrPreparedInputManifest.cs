using System.Collections.Generic;

namespace PocketDeck.SteamVR;

internal sealed record OpenVrPreparedInputManifest(string ActionManifestPath, IReadOnlyList<OpenVrPreparedBindingProfile> Bindings);
