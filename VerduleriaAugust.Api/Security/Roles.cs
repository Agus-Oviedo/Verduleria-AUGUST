namespace VerduleriaAugust.Api.Security;

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Encargado = "Encargado";
    public const string Cajero = "Cajero";

    public const string Administracion = Administrador + "," + Encargado;
    public const string OperacionVenta = Administrador + "," + Encargado + "," + Cajero;

    public static bool EsRolHumanoValido(string role) =>
        role is Administrador or Encargado or Cajero;
}
