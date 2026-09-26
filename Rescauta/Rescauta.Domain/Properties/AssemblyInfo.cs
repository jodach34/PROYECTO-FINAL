using System.Runtime.CompilerServices;

// Infrastructure necesita escribir los campos de auditoria (CreatedAt, UpdatedBy, ...)
// que en BaseEntity tienen setter internal. Se limita la exposicion SOLO a las dos capas
// que legitimately participan: la capa que persiste y la capa que lee.
[assembly: InternalsVisibleTo("Rescauta.Infrastructure")]
[assembly: InternalsVisibleTo("Rescauta.Application")]
