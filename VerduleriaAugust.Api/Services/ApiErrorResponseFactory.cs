using Microsoft.AspNetCore.Mvc;
using VerduleriaAugust.Api.DTOs;

namespace VerduleriaAugust.Api.Services;

public static class ApiErrorResponseFactory
{
    public static BadRequestObjectResult FromInvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => string.IsNullOrWhiteSpace(entry.Key) ? "solicitud" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "El valor enviado no es válido."
                        : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return new BadRequestObjectResult(new ApiErrorResponse(
            "Uno o más campos no son válidos.",
            "VALIDACION",
            context.HttpContext.TraceIdentifier,
            errors));
    }
}
