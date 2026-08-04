using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public CategoriasController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Categoria>>> GetCategorias()
    {
        var categorias = await _context.Categorias
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Categoria>> GetCategoria(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);

        if (categoria == null)
            return NotFound();

        return Ok(categoria);
    }
}