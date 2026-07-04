namespace AltarWeb.Models.Registro
{
    public class CatalogoGenero
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }
}
