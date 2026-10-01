using TicketFlow.Application.Common;

namespace TicketFlow.Application.Auth;

public class EmailAlreadyRegisteredException()
    : ConflictException("EMAIL_ALREADY_REGISTERED", "The email is already registered.");
