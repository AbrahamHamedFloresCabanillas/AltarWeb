namespace AltarWeb.ViewModels.Altar
{
    public class AltarNavContext
    {
        public string ActiveItem { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool EsAdmin { get; set; }

        public string Iniciales =>
            string.Join(string.Empty, NombreCompleto
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0])));
    }
}
