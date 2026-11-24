using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Identity.Application.Departments;

public sealed class DepartmentConflictException(string message) : ConflictException(message);
