namespace PocketDeck.Contracts;

public sealed record SharingSessionSnapshot(SharingMode Mode, SharingRole Role, int ViewerCount, string StateCode);
