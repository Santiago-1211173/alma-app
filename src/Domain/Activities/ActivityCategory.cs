using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace AlmaApp.Domain.Activities
{
    /// <summary>
    /// Categoria da actividade. Por agora suportamos apenas workshops, mas
    /// poderão ser adicionadas outras categorias no futuro. Esta enumeração
    /// permite filtrar actividades por tipo.
    /// </summary>
    public enum ActivityCategory
    {
        /// <summary>
        /// Representa um workshop. As actividades deste tipo requerem inscrição
        /// prévia e têm um número máximo de participantes.
        /// </summary>
        Workshop = 1
    }
}