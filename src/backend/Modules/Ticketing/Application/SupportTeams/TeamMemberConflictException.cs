using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Application.SupportTeams;

public sealed class TeamMemberConflictException : ConflictException
{
    public TeamMemberConflictException(string message) : base(message)
    {
    }
}
