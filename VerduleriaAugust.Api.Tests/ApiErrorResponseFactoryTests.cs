using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Tests;

public class ApiErrorResponseFactoryTests
{
    [Fact]
    public void ModelStateInvalido_DevuelveContratoUniforme()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-prueba" };
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("CajaId", "Debe ser mayor a cero.");
        modelState.AddModelError("CajaId", "Debe ser mayor a cero.");
        modelState.AddModelError("Items", "Debe contener al menos un elemento.");
        var actionContext = new ActionContext(
            httpContext, new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(), modelState);

        var result = ApiErrorResponseFactory.FromInvalidModelState(actionContext);

        var error = Assert.IsType<ApiErrorResponse>(result.Value);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("trace-prueba", error.TraceId);
        Assert.Equal(2, error.Errores!.Count);
        Assert.Single(error.Errores["CajaId"]);
    }
}
