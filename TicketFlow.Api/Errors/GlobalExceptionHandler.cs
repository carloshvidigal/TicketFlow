using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using TicketFlow.Application.Common;
using TicketFlow.Domain.Common;

namespace TicketFlow.Api.Errors;

// Tratamento centralizado de erros (§17): nenhum controller precisa de
// try/catch, e o cliente sempre recebe o mesmo formato. Erros inesperados
// nunca vazam mensagem ou stack trace — só vão para o log.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Cliente desistiu da requisição: não é erro do servidor.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, error) = exception switch
        {
            ValidationException ex => (
                StatusCodes.Status400BadRequest,
                new ErrorBody(
                    "VALIDATION_ERROR",
                    "One or more validation errors occurred.",
                    ex.Errors.Select(e => new ErrorDetail(ToCamelCase(e.PropertyName), e.ErrorMessage)).ToList())),

            UnauthorizedException ex => (
                StatusCodes.Status401Unauthorized,
                new ErrorBody(ex.Code, ex.Message)),

            ConflictException ex => (
                StatusCodes.Status409Conflict,
                new ErrorBody(ex.Code, ex.Message)),

            DomainException ex => (
                StatusCodes.Status422UnprocessableEntity,
                new ErrorBody(ex.Code, ex.Message)),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorBody("INTERNAL_ERROR", "An unexpected error occurred."))
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(error), cancellationToken);

        return true;
    }

    // Os campos do JSON da Api são camelCase; o erro deve apontar "email",
    // não "Email".
    internal static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
