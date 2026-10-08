using System.Threading;
using System.Threading.Tasks;
using PocketDeck.Contracts;

namespace PocketDeck.Input;

public delegate ValueTask PhoneInputCommandSender(PhoneInputCommand command, CancellationToken cancellationToken);
