Generate a manual mapper for the mapping described in $ARGUMENTS.

$ARGUMENTS format: "SourceClass -> DestinationClass [style]" (e.g. "Order -> OrderDto extension"
or "Order -> OrderDto di").
- The optional `style` token selects the mapper shape: `extension` (an extension method) or
  `di` (a DI-registered mapper). It may appear anywhere in the arguments.
- If the source/destination classes are missing, ask the user for them.
- **If the style is not provided, ask the user** whether they want an **extension method** mapper or a
  **DI mapper** before generating anything.
- **Also ask the user for a preferred work location** (the directory/file the mapper should be written
  to). If they have no preference, infer a sensible location from where existing mappers/models live in
  the codebase.
- If a destination member has multiple plausible source members, **ask the user instead of guessing**.
- If a nested source path may be null and the destination member is non-nullable, do not silently invent
  a fallback. Use the project's existing null-handling pattern when one exists; otherwise ask the user.
- Only apply trivial safe conversions automatically (e.g. exact type match, nullable-to-nullable, or
  obvious string passthrough). For non-trivial conversions, formatting, enums, or lossy casts, ask the
  user or clearly report the member as skipped.

Follow these steps:

1. **Find the source class**: Search the codebase for a class named `<SourceClass>`. Read its full
   definition including all properties and their types. Also read any nested types it references.

2. **Find or create the destination class**: Search for `<DestinationClass>`. If it exists, read it.
   If it does not exist, infer reasonable property names and types from the source and create it as
   a new file, matching the existing model/POCO style in the codebase.

3. **Match the project's conventions**: Before writing, look at an existing mapper (and the source and
  destination classes) to match the namespace, `using` directives, formatting, naming, and comment
  style exactly. If the repository already uses a manual core mapper plus a DI wrapper, prefer that
  convention over inventing a new naming pattern.

4. **Generate the mapper** in the chosen work location, named `<SourceClass>Mapper.cs`.

   For nested source properties that flatten to a flat destination (e.g. `src.Address.City` →
   `dest.City`), write the full path.

   Every destination property must end up in one of three buckets: mapped explicitly, skipped and
   reported, or blocked pending user clarification. Do not leave ambiguous members unmentioned.

   - **If the style is `extension`**: write a `static class <SourceClass>MapperExtensions` containing a
     `public static <DestinationClass> To<DestinationClass>(this <SourceClass> src)` extension method
     that explicitly assigns each destination property. Do not generate an interface or service class.

   - **If the style is `di`**: write
     - A `public interface I<SourceClass>Mapper` with a single `<DestinationClass> Map(<SourceClass> src)` method.
     - A `public class <SourceClass>MapperService : I<SourceClass>Mapper` that explicitly assigns each
       destination property, suitable for DI registration as a singleton.
     - If the local convention is a reusable static mapper plus a DI wrapper, keep the mapping logic in
       the reusable mapper and let the service delegate to it.

5. **Register the DI mapper** (only when style is `di`): add the registration to the application's
   service provider. Search the codebase for where services are registered (e.g. a `ServiceCollection`
   in `Program.cs`/`Startup.cs`, a DI extension method, or composition root) and add:
   `services.AddSingleton<I<SourceClass>Mapper, <SourceClass>MapperService>();`
   matching the surrounding registration style. If there is no obvious place to register it, ask the
   user where the mapper should be registered.

6. **Validate**: After writing the mapper, run the narrowest available validation for the touched slice
   (prefer a focused build or type-check for the affected project; otherwise use the editor error list).
   If validation fails, fix the mapper before reporting back.

7. **Report back**: List every property mapped, call out any that were skipped (e.g. complex types,
   ambiguous matches, nullable-path issues, or members with no obvious destination equivalent), state
   the chosen style and work location, include the validation command/result, and:
   - For `extension`: show the call site, e.g. `var dto = order.ToOrderDto();`
   - For `di`: show the registration one-liner,
     `services.AddSingleton<I<SourceClass>Mapper, <SourceClass>MapperService>();`, and confirm where it
     was added.