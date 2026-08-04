using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// CONTROLLERS
// ======================================================
builder.Services.AddControllers();

// ======================================================
// SWAGGER SIMPLE
// ======================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ======================================================
// BASE DE DATOS - AugustDb
// ======================================================
builder.Services.AddDbContext<AugustDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ======================================================
// CORS
// ======================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// ======================================================
// SWAGGER
// ======================================================
app.UseSwagger();
app.UseSwaggerUI();

// ======================================================
// RUTA DE PRUEBA
// ======================================================
app.MapGet("/", () => "API Verduleria August funcionando correctamente");

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();