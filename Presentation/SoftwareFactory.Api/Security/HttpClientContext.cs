using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Api.Security;

/// <summary>Remote address and user agent of the current request, for the audit trail (estandar-auth.md §6).</summary>
internal sealed class HttpClientContext(IHttpContextAccessor accessor) : IClientContext
{
    public AuditClient Client
    {
        get
        {
            var context = accessor.HttpContext;
            return new AuditClient(context?.Connection.RemoteIpAddress?.ToString(), context?.Request.Headers.UserAgent.ToString());
        }
    }
}
