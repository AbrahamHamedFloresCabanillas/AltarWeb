// Cada fabrica configura la app con variables de entorno del proceso (LocalDB desechable propia);
// en paralelo dos fabricas compartirian base.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
