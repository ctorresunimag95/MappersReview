using MappersComparasion.DI;

Console.WriteLine("========================================");
Console.WriteLine("  Mapper DI Capabilities Demo");
Console.WriteLine("========================================");

Console.WriteLine("\n--- Manual (interface registration) ---");
ManualDIDemo.Run();

Console.WriteLine("\n--- Mapster (IMapper + TypeAdapterConfig) ---");
MapsterDIDemo.Run();

Console.WriteLine("\n--- Mapperly (register generated class; supports ctor injection) ---");
MapperlyDIDemo.Run();

Console.WriteLine("\n--- Facet (generated constructor; DI via wrapper service) ---");
FacetDIDemo.Run();

Console.WriteLine("\n========================================");
