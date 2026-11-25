using System;
using System.Collections.Generic;

namespace TechStore.DatabaseFirst.Models;

public partial class Cliente
{
    public int IdCliente { get; set; }

    public string Cedula { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Email { get; set; } = null!;

    public virtual ICollection<Venta> Venta { get; set; } = new List<Venta>();
}
