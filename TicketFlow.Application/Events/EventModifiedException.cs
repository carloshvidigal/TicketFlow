using TicketFlow.Application.Common;

namespace TicketFlow.Application.Events;

public class EventModifiedException()
    : ConflictException("EVENT_MODIFIED", "The event was modified by another request. Reload it and try again.");

public class SectionNameAlreadyExistsException()
    : ConflictException("SECTION_NAME_ALREADY_EXISTS", "This event already has a section with that name.");
