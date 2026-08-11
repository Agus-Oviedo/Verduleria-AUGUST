using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Services;
using VerduleriaAugust.Api.Security;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AugustDbContext _context;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly JwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(
        AugustDbContext context,
        IPasswordHasher<Usuario> passwordHasher,
        JwtTokenService tokenService,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("Autenticacion")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var normalizedName = NormalizeUserName(dto.NombreUsuario);
        var usuario = await _context.Usuarios.SingleOrDefaultAsync(u =>
            u.NombreUsuarioNormalizado == normalizedName && u.Activo);

        if (usuario == null ||
            _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.Password) ==
            PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
        }

        var (token, expira) = _tokenService.CreateToken(usuario);
        return Ok(new
        {
            token,
            expira,
            usuario = new { usuario.Id, usuario.NombreUsuario, usuario.Rol, usuario.DebeCambiarPassword }
        });
    }

    [AllowAnonymous]
    [HttpPost("bootstrap")]
    [EnableRateLimiting("Autenticacion")]
    public async Task<IActionResult> CrearPrimerAdministrador([FromBody] BootstrapAdminDto dto)
    {
        if (await _context.Usuarios.AnyAsync())
            return Conflict(new { mensaje = "El administrador inicial ya fue creado." });

        var expectedKey = _configuration["Auth:BootstrapKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) || !FixedTimeEquals(dto.ClaveInicial, expectedKey))
            return Unauthorized(new { mensaje = "La clave inicial no es válida." });

        var validationError = ValidateCredentials(dto.NombreUsuario, dto.Password);
        if (validationError != null)
            return BadRequest(new { mensaje = validationError });

        var usuario = CreateUser(dto.NombreUsuario, dto.Password, "Administrador");
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            mensaje = "Administrador inicial creado correctamente.",
            usuario.Id,
            usuario.NombreUsuario,
            usuario.Rol
        });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("usuarios")]
    public async Task<IActionResult> CrearUsuario([FromBody] CrearUsuarioDto dto)
    {
        var validationError = ValidateCredentials(dto.NombreUsuario, dto.Password);
        if (validationError != null)
            return BadRequest(new { mensaje = validationError });

        if (!Roles.EsRolHumanoValido(dto.Rol))
            return BadRequest(new { mensaje = "El rol debe ser Administrador, Encargado o Cajero." });

        var normalizedName = NormalizeUserName(dto.NombreUsuario);
        if (await _context.Usuarios.AnyAsync(u => u.NombreUsuarioNormalizado == normalizedName))
            return Conflict(new { mensaje = "El nombre de usuario ya está registrado." });

        var usuario = CreateUser(dto.NombreUsuario, dto.Password, dto.Rol);
        _context.Usuarios.Add(usuario);
        AddAudit(usuario, "UsuarioCreado", $"Usuario creado con rol {dto.Rol} mediante alta administrativa.");
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            mensaje = "Usuario creado correctamente.",
            usuario.Id,
            usuario.NombreUsuario,
            usuario.Rol
        });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("usuarios/invitacion")]
    public async Task<IActionResult> InvitarUsuario([FromBody] InvitarUsuarioDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreUsuario) || dto.NombreUsuario.Trim().Length < 3)
            return BadRequest(new { mensaje = "El nombre de usuario debe tener al menos 3 caracteres." });
        if (!Roles.EsRolHumanoValido(dto.Rol)) return BadRequest(new { mensaje = "Rol inválido." });
        var normalized = NormalizeUserName(dto.NombreUsuario);
        if (await _context.Usuarios.AnyAsync(u => u.NombreUsuarioNormalizado == normalized))
            return Conflict(new { mensaje = "El nombre de usuario ya está registrado." });
        var temporaryPassword = GenerateTemporaryPassword();
        var usuario = CreateUser(dto.NombreUsuario, temporaryPassword, dto.Rol);
        usuario.DebeCambiarPassword = true;
        _context.Usuarios.Add(usuario);
        AddAudit(usuario, "UsuarioInvitado", $"Usuario creado con rol {dto.Rol} y acceso temporal.");
        await _context.SaveChangesAsync();
        return StatusCode(201, new { mensaje = "Usuario creado. La contraseña temporal se muestra una sola vez.", usuario.Id, usuario.NombreUsuario, usuario.Rol, passwordTemporal = temporaryPassword });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("usuarios/{id:int}/restablecer-password")]
    public async Task<IActionResult> RestablecerPassword(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        var temporaryPassword = GenerateTemporaryPassword();
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, temporaryPassword);
        usuario.DebeCambiarPassword = true; usuario.Activo = true;
        AddAudit(usuario, "AccesoRestablecido", "Se generó un nuevo acceso temporal y el usuario quedó activo.");
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Contraseña restablecida.", passwordTemporal = temporaryPassword });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("usuarios")]
    public async Task<IActionResult> GetUsuarios() => Ok(await _context.Usuarios.AsNoTracking()
        .OrderBy(u => u.NombreUsuario)
        .Select(u => new { u.Id, u.NombreUsuario, u.Rol, u.Activo, u.FechaCreacion })
        .ToListAsync());

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("usuarios/auditoria")]
    public async Task<IActionResult> GetAuditoriaUsuarios([FromQuery] AuditoriaUsuariosConsultaDto consulta)
    {
        var query = _context.AuditoriasUsuario.AsNoTracking().AsQueryable();
        if (consulta.UsuarioId.HasValue)
            query = query.Where(audit => audit.UsuarioObjetivoId == consulta.UsuarioId.Value);
        if (!string.IsNullOrWhiteSpace(consulta.Accion))
            query = query.Where(audit => audit.Accion == consulta.Accion);
        if (consulta.Desde.HasValue)
            query = query.Where(audit => audit.Fecha >= consulta.Desde.Value);
        if (consulta.Hasta.HasValue)
            query = query.Where(audit => audit.Fecha <= consulta.Hasta.Value);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(audit => audit.Fecha)
            .ThenByDescending(audit => audit.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(audit => new AuditoriaUsuarioDto(
                audit.Id,
                audit.UsuarioObjetivoId,
                audit.UsuarioObjetivo!.NombreUsuario,
                audit.UsuarioActorId,
                audit.UsuarioActor != null ? audit.UsuarioActor.NombreUsuario : "Sistema",
                audit.Accion,
                audit.Detalle,
                audit.Fecha))
            .ToListAsync();
        return Ok(new RespuestaPaginada<AuditoriaUsuarioDto>(
            items, total, consulta.Pagina, consulta.TamanoPagina));
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPut("usuarios/{id:int}")]
    public async Task<IActionResult> ActualizarUsuario(int id, [FromBody] ActualizarUsuarioDto dto)
    {
        if (!Roles.EsRolHumanoValido(dto.Rol))
            return BadRequest(new { mensaje = "El rol debe ser Administrador, Encargado o Cajero." });
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        var currentIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(currentIdValue, out var currentId) && currentId == id && !dto.Activo)
            return Conflict(new { mensaje = "No podés desactivar tu propio usuario." });
        if (usuario.Rol == Roles.Administrador && (dto.Rol != Roles.Administrador || !dto.Activo) &&
            await _context.Usuarios.CountAsync(u => u.Rol == Roles.Administrador && u.Activo) <= 1)
            return Conflict(new { mensaje = "Debe existir al menos un administrador activo." });
        var cambios = new List<string>();
        if (usuario.Rol != dto.Rol) cambios.Add($"Rol: {usuario.Rol} → {dto.Rol}");
        if (usuario.Activo != dto.Activo) cambios.Add($"Estado: {(usuario.Activo ? "Activo" : "Inactivo")} → {(dto.Activo ? "Activo" : "Inactivo")}");
        usuario.Rol = dto.Rol; usuario.Activo = dto.Activo;
        if (cambios.Count > 0)
            AddAudit(usuario, "UsuarioActualizado", string.Join(". ", cambios) + ".");
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Usuario actualizado correctamente." });
    }

    [Authorize]
    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var usuario = await _context.Usuarios.SingleOrDefaultAsync(u => u.Id == userId && u.Activo);
        if (usuario == null)
            return Unauthorized(new { mensaje = "El usuario no existe o está inactivo." });

        if (_passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.PasswordActual) ==
            PasswordVerificationResult.Failed)
        {
            return BadRequest(new { mensaje = "La contraseña actual no es correcta." });
        }

        if (string.IsNullOrWhiteSpace(dto.PasswordNueva) || dto.PasswordNueva.Length < 10)
            return BadRequest(new { mensaje = "La contraseña nueva debe tener al menos 10 caracteres." });

        if (dto.PasswordActual == dto.PasswordNueva)
            return BadRequest(new { mensaje = "La contraseña nueva debe ser diferente de la actual." });

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, dto.PasswordNueva);
        usuario.DebeCambiarPassword = false;
        _context.AuditoriasUsuario.Add(new AuditoriaUsuario
        {
            UsuarioObjetivo = usuario,
            UsuarioActor = usuario,
            Accion = "PasswordCambiada",
            Detalle = "El usuario cambió su propia contraseña."
        });
        await _context.SaveChangesAsync();

        var (token, expira) = _tokenService.CreateToken(usuario);
        return Ok(new { mensaje = "Contraseña actualizada correctamente.", token, expira });
    }

    private Usuario CreateUser(string userName, string password, string role)
    {
        var usuario = new Usuario
        {
            NombreUsuario = userName.Trim(),
            NombreUsuarioNormalizado = NormalizeUserName(userName),
            Rol = role,
            Activo = true,
            FechaCreacion = DateTime.Now
        };
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, password);
        return usuario;
    }

    private static string? ValidateCredentials(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length < 3)
            return "El nombre de usuario debe tener al menos 3 caracteres.";
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            return "La contraseña debe tener al menos 10 caracteres.";
        return null;
    }

    private static string NormalizeUserName(string userName) =>
        userName.Trim().ToUpperInvariant();

    private static string GenerateTemporaryPassword() =>
        $"Aug-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}!";

    private void AddAudit(Usuario target, string action, string detail)
    {
        _context.AuditoriasUsuario.Add(new AuditoriaUsuario
        {
            UsuarioObjetivo = target,
            UsuarioActorId = CurrentUserId(),
            Accion = action,
            Detalle = detail
        });
    }

    private int? CurrentUserId()
    {
        var value = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
