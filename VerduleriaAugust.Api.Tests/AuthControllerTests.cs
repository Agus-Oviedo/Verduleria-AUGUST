using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Tests;

public class AuthControllerTests
{
    private const string BootstrapKey = "clave-inicial-segura-para-pruebas";

    [Fact]
    public async Task Bootstrap_Valido_CreaAdministradorConPasswordHasheada()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var result = await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin",
            Password = "PasswordSegura123!",
            ClaveInicial = BootstrapKey
        });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        var usuario = await context.Usuarios.SingleAsync();
        Assert.Equal("ADMIN", usuario.NombreUsuarioNormalizado);
        Assert.Equal("Administrador", usuario.Rol);
        Assert.NotEqual("PasswordSegura123!", usuario.PasswordHash);
    }

    [Fact]
    public async Task Bootstrap_SiYaExisteUsuario_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        var dto = new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        };
        await controller.CrearPrimerAdministrador(dto);

        var result = await controller.CrearPrimerAdministrador(dto);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.Usuarios);
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_DevuelveJwtConRol()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        });

        var result = await controller.Login(new LoginDto
        {
            NombreUsuario = "ADMIN", Password = "PasswordSegura123!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var tokenProperty = ok.Value!.GetType().GetProperty("token")!;
        var token = Assert.IsType<string>(tokenProperty.GetValue(ok.Value));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, claim =>
            claim.Type == ClaimTypes.Role && claim.Value == "Administrador");
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_DevuelveUnauthorized()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        });

        var result = await controller.Login(new LoginDto
        {
            NombreUsuario = "admin", Password = "incorrecta"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CrearUsuario_ConRolInvalido_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await CreateController(context).CrearUsuario(new CrearUsuarioDto
        {
            NombreUsuario = "operador", Password = "PasswordSegura123!", Rol = "Supervisor"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Usuarios);
    }

    [Fact]
    public async Task CambiarPassword_ConPasswordActualCorrecta_ReemplazaElHash()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        });
        var usuario = await context.Usuarios.SingleAsync();
        SetAuthenticatedUser(controller, usuario.Id);

        var result = await controller.CambiarPassword(new CambiarPasswordDto
        {
            PasswordActual = "PasswordSegura123!",
            PasswordNueva = "PasswordNueva456!"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!"
        }));
        Assert.IsType<OkObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = "admin", Password = "PasswordNueva456!"
        }));
    }

    [Fact]
    public async Task CambiarPassword_ConPasswordActualIncorrecta_NoCambiaElHash()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        });
        var usuario = await context.Usuarios.SingleAsync();
        var originalHash = usuario.PasswordHash;
        SetAuthenticatedUser(controller, usuario.Id);

        var result = await controller.CambiarPassword(new CambiarPasswordDto
        {
            PasswordActual = "PasswordIncorrecta!",
            PasswordNueva = "PasswordNueva456!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(originalHash, usuario.PasswordHash);
    }

    [Fact]
    public async Task Invitacion_YPrimerCambio_ReemplazanPasswordTemporalYRenuevanToken()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var invitation = await controller.InvitarUsuario(new InvitarUsuarioDto
        {
            NombreUsuario = "caja-prueba",
            Rol = "Cajero"
        });

        var created = Assert.IsType<ObjectResult>(invitation);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var temporaryPassword = Assert.IsType<string>(
            created.Value!.GetType().GetProperty("passwordTemporal")!.GetValue(created.Value));
        var usuario = await context.Usuarios.SingleAsync();
        Assert.True(usuario.DebeCambiarPassword);

        var temporaryLogin = Assert.IsType<OkObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = "caja-prueba",
            Password = temporaryPassword
        }));
        var temporaryToken = Assert.IsType<string>(
            temporaryLogin.Value!.GetType().GetProperty("token")!.GetValue(temporaryLogin.Value));
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(temporaryToken).Claims,
            claim => claim.Type == JwtTokenService.PasswordChangeRequiredClaim && claim.Value == "true");

        SetAuthenticatedUser(controller, usuario.Id);
        var changed = Assert.IsType<OkObjectResult>(await controller.CambiarPassword(new CambiarPasswordDto
        {
            PasswordActual = temporaryPassword,
            PasswordNueva = "ClavePrivadaNueva456!"
        }));

        Assert.False(usuario.DebeCambiarPassword);
        var renewedToken = Assert.IsType<string>(
            changed.Value!.GetType().GetProperty("token")!.GetValue(changed.Value));
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(renewedToken).Claims,
            claim => claim.Type == JwtTokenService.PasswordChangeRequiredClaim && claim.Value == "false");
        Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = "caja-prueba", Password = temporaryPassword
        }));
        Assert.IsType<OkObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = "caja-prueba", Password = "ClavePrivadaNueva456!"
        }));
    }

    [Fact]
    public async Task RestablecerPassword_ReactivaUsuarioYExigeNuevoCambio()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        var usuario = new Usuario
        {
            NombreUsuario = "caja-inactiva",
            NombreUsuarioNormalizado = "CAJA-INACTIVA",
            PasswordHash = "hash-anterior",
            Rol = "Cajero",
            Activo = false
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await controller.RestablecerPassword(usuario.Id));
        var temporaryPassword = Assert.IsType<string>(
            result.Value!.GetType().GetProperty("passwordTemporal")!.GetValue(result.Value));

        Assert.True(usuario.Activo);
        Assert.True(usuario.DebeCambiarPassword);
        Assert.NotEqual("hash-anterior", usuario.PasswordHash);
        Assert.IsType<OkObjectResult>(await controller.Login(new LoginDto
        {
            NombreUsuario = usuario.NombreUsuario,
            Password = temporaryPassword
        }));
    }

    [Fact]
    public async Task GestionUsuarios_RegistraAuditoriaSinGuardarClavesTemporales()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);
        await controller.CrearPrimerAdministrador(new BootstrapAdminDto
        {
            NombreUsuario = "admin", Password = "PasswordSegura123!", ClaveInicial = BootstrapKey
        });
        var admin = await context.Usuarios.SingleAsync();
        SetAuthenticatedUser(controller, admin.Id);

        var invitation = Assert.IsType<ObjectResult>(await controller.InvitarUsuario(
            new InvitarUsuarioDto { NombreUsuario = "auditado", Rol = "Cajero" }));
        var temporaryPassword = Assert.IsType<string>(
            invitation.Value!.GetType().GetProperty("passwordTemporal")!.GetValue(invitation.Value));
        var target = await context.Usuarios.SingleAsync(user => user.NombreUsuario == "auditado");
        await controller.ActualizarUsuario(target.Id,
            new ActualizarUsuarioDto { Rol = "Encargado", Activo = false });
        await controller.RestablecerPassword(target.Id);

        var auditResult = Assert.IsType<OkObjectResult>(await controller.GetAuditoriaUsuarios(
            new AuditoriaUsuariosConsultaDto { UsuarioId = target.Id }));
        var page = Assert.IsType<RespuestaPaginada<AuditoriaUsuarioDto>>(auditResult.Value);

        Assert.Equal(3, page.Total);
        Assert.Contains(page.Items, audit => audit.Accion == "UsuarioInvitado");
        Assert.Contains(page.Items, audit => audit.Accion == "UsuarioActualizado");
        Assert.Contains(page.Items, audit => audit.Accion == "AccesoRestablecido");
        Assert.All(page.Items, audit => Assert.DoesNotContain(temporaryPassword, audit.Detalle));
        Assert.All(page.Items, audit => Assert.Equal("admin", audit.UsuarioActor));
    }

    private static AuthController CreateController(AugustDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "clave-jwt-de-pruebas-con-mas-de-32-caracteres-seguros",
                ["Jwt:Issuer"] = "VerduleriaAugust.Api.Tests",
                ["Jwt:Audience"] = "VerduleriaAugust.Tests",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Auth:BootstrapKey"] = BootstrapKey
            })
            .Build();
        var hasher = new PasswordHasher<Usuario>();
        return new AuthController(
            context,
            hasher,
            new JwtTokenService(configuration),
            configuration);
    }

    private static void SetAuthenticatedUser(AuthController controller, int userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "TestAuthentication");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
