# Manual Mapping Guidance

Practical guidance for writing **manual mappers** in this codebase — mapping one type
(e.g. a domain model) onto another (e.g. a DTO) by hand, without a mapping library.

This complements the `/generate-mapper` command (`.claude/commands/generate-mapper.md`) and
the matching prompt (`.github/prompts/generate-mapper.prompt.md`), which automate producing a
mapper. This document explains the *practices* those tools follow so you can write, review, or
extend mappers consistently — whether by hand or with AI assistance.

---

## Core principle: mappers do mapping, nothing else

> **A mapper is pure translation. It takes a source object and returns a destination object.
> It must not contain business logic.**

A mapper should be predictable and side-effect free:

- ✅ Copy values across (`dest.FirstName = src.FirstName`).
- ✅ Flatten nested paths (`dest.City = src.Address.City`).
- ✅ Apply *trivial, lossless* conversions (exact type match, nullable→nullable, obvious string
  passthrough).
- ❌ **No** validation, business rules, or decisions ("if the user is a VIP, set X").
- ❌ **No** I/O — no database calls, HTTP, file access, logging-as-behavior.
- ❌ **No** mutation of the source, and no shared/global state.
- ❌ **No** non-trivial transforms hidden inside the mapper (currency math, enum reinterpretation,
  date formatting for display). Those belong in the domain/service layer; the mapper receives the
  already-computed value.

If a destination value requires a *decision* or a *calculation*, compute it upstream and pass it
in — keep the mapper a flat, readable list of assignments. This makes mappers trivial to read,
diff, and verify against the two class definitions.

---

## The two supported shapes

This repo recognizes two manual-mapping styles. Pick based on how the mapper is consumed.

| Style | When to use | Shape |
|-------|-------------|-------|
| **Extension method** | Simple, stateless mapping; ergonomic call site; no dependencies needed | `static` class with a `To<Dest>()` extension method |
| **Injectable (DI) mapper** | The mapper is a dependency that gets injected, mocked, or swapped; fits a DI composition root | An interface + a service class registered in the container |

Both styles assign **every** destination property explicitly. Nothing is mapped implicitly.

---

## When to use a DI injectable mapper

Default to the **extension method** — it's lighter and most mappings are pure, dependency-free
functions. Escalate to a **DI injectable mapper** only when something forces the mapper to behave
like an *injectable dependency* rather than a static helper.

**Reach for the DI mapper when:**

- **The mapper has its own dependencies.** Even with no business logic, a mapper sometimes
  legitimately needs an injected collaborator — an `IClock`/time provider, an `IConfiguration`
  value, an `IStringLocalizer` for display strings, or a child mapper (`IAddressMapper`) it
  composes. A static extension method can't receive those cleanly; constructor injection can.
- **You want to swap or mock it in tests.** If a class under test depends on the mapping and you
  want to substitute a fake mapper (or assert it was called), injecting `IUserMapper` makes that
  trivial. An extension method is static and can't be mocked.
- **The mapping is selected at runtime.** Multiple implementations behind one interface (e.g.
  `LegacyUserMapper` vs `V2UserMapper`), chosen via DI registration, keyed services, or a factory.
- **It fits an existing composition root.** If the consuming code already receives everything via
  constructor injection, an injected mapper keeps the style consistent and makes the dependency
  explicit in the constructor signature.
- **You want a seam for cross-cutting concerns.** Decorators (e.g. caching or instrumentation
  around the mapper) are easy when it's interface-based.

**Stick with the extension method when:**

- The mapping is **stateless and self-contained** — it just reads source properties and assigns
  them (like `User` → `UserDto` here).
- You want the **ergonomic inline call site** (`user.ToUserDto()`) with zero wiring.
- The mapper has **no dependencies** and there's no real need to mock it (you can test the pure
  function directly).

> **Nuance:** going DI does **not** mean putting logic in the service. In this repo the DI style
> keeps the actual assignments in the reusable static core mapper (`ManualMapper.Map`) and the
> injectable `ManualUserMapper` just delegates to it. The interface exists only to provide the
> injectable seam — the mapping logic stays in one pure place.

---

### Style 1 — Extension method

Best for the common case: a stateless, ergonomic conversion you call inline.

```csharp
using MappersComparasion.Models;

namespace MappersComparasion.Mappers;

public static class UserMapperExtensions
{
    public static UserDto ToUserDto(this User src) => new()
    {
        Id        = src.Id,
        FirstName = src.FirstName,
        LastName  = src.LastName,
        City      = src.Address.City   // nested path flattened to a flat destination
    };
}
```

Call site:

```csharp
var dto = user.ToUserDto();
```

Guidelines:

- Name the class `<Source>MapperExtensions` and the method `To<Destination>()`.
- Do **not** add an interface or service class for this style — the whole point is the lightweight,
  static call site.

---

### Style 2 — Injectable (DI) mapper

Best when the mapper is a collaborator that should be injected and substitutable (e.g. for tests,
or when different mappings are selected at runtime).

This codebase's convention (see [ManualMapper.cs](MappersComparasion.Shared/Mappers/ManualMapper.cs))
is a **reusable static core mapper plus a thin DI wrapper** that delegates to it. Keep the mapping
logic in one place and let the service forward to it — don't duplicate the assignments.

```csharp
using MappersComparasion.Models;

namespace MappersComparasion.Mappers;

// Reusable core mapper — the single source of truth for the assignments.
public static class ManualMapper
{
    public static UserDto Map(User src) => new()
    {
        Id        = src.Id,
        FirstName = src.FirstName,
        LastName  = src.LastName,
        City      = src.Address.City
    };
}

public interface IUserMapper
{
    UserDto Map(User src);
}

// Thin DI wrapper — delegates, holds no logic of its own.
public class ManualUserMapper : IUserMapper
{
    public UserDto Map(User src) => ManualMapper.Map(src);
}
```

Registration (singleton — the mapper is stateless, so one instance is safe to share):

```csharp
services.AddSingleton<IUserMapper, ManualUserMapper>();
```

See [ManualDIDemo.cs](MappersComparasion.DI/DI/ManualDIDemo.cs) for a working registration +
resolution example.

Guidelines:

- Interface: `I<Source>Mapper` with a single `<Destination> Map(<Source> src)`.
- Implementation: `<Source>MapperService` (or the repo's existing `Manual<Source>Mapper` name) —
  match what's already there rather than inventing a new pattern.
- Register as a **singleton** because the mapper carries no state.

---

## Handling the awkward cases (don't guess)

These are the situations where mapping stops being mechanical. The rule is the same one the
`/generate-mapper` command follows: **be explicit, and ask rather than invent.**

- **Ambiguous source** — if a destination property has more than one plausible source, don't pick
  one silently. Confirm which source member is correct.
- **Nullable source → non-nullable destination** — if a nested path can be null
  (`src.Address?.City`) and the destination is non-nullable, do **not** invent a silent fallback.
  Use the project's existing null-handling pattern; if none exists, ask.
- **Non-trivial conversions** — enums, formatting, lossy casts, currency/units. Either compute the
  value upstream and pass it in, or **clearly report the member as skipped** — never bury a
  conversion inside the mapper.
- **Account for every destination property.** Each one must end up in exactly one bucket:
  1. **Mapped** explicitly, or
  2. **Skipped** and reported (with the reason), or
  3. **Blocked** pending clarification.

  No destination member should be left unmentioned.

---

## Conventions checklist

Before committing a manual mapper:

- [ ] Mapper contains **only** assignments — no business logic, I/O, or side effects.
- [ ] Namespace, `using` directives, naming, and formatting match an existing mapper.
- [ ] Every destination property is mapped, skipped-and-reported, or blocked.
- [ ] Nested flattening uses the full path (`src.Address.City`).
- [ ] Style matches intent (extension for inline use; DI for an injectable dependency).
- [ ] DI mappers are registered (`AddSingleton`) and delegate to the reusable core mapper.
- [ ] The touched project still builds / type-checks.

---

## Using AI assistance

AI assistance is welcome for producing and reviewing manual mappers — that's exactly what the
`/generate-mapper` command and `generate-mapper.prompt.md` are for. AI is well suited to the
tedious, mechanical part: enumerating every destination property, matching it to a source member,
flattening nested paths, and matching the repo's existing style.

When you lean on AI for mapping:

- **Let it ask.** A good mapping assistant should *stop and ask* on ambiguous sources, nullable
  paths, and non-trivial conversions instead of guessing — the same rules above apply to it.
- **Keep it to pure mapping.** Don't let generated code smuggle in business logic; if the AI
  proposes a calculation or rule inside the mapper, move that logic upstream.
- **You still own the result.** Verify every assignment against the two class definitions, confirm
  skipped members were intended to be skipped, and make sure it builds before merging.

In short: AI does the mechanical mapping fast; you verify it stayed *pure mapping* and that the
judgment calls were made (or surfaced), not silently invented.
