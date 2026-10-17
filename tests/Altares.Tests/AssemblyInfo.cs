// Las pruebas de integracion configuran la app con variables de entorno del proceso (cadena de conexion
// de una LocalDB desechable por fabrica); correrlas en paralelo haria que dos fabricas compartieran base.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
