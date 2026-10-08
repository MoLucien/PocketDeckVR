namespace PocketDeck.Contracts;

public readonly record struct StateReason(string Code, string Message)
{
	public static StateReason Normal(string code, string message)
	{
		return new StateReason(code, message);
	}
}
