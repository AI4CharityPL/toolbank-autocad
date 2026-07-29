# MCPBank + MCP Discovery + acad-router — Dowód techniczny

> **Status:** production-ready (v0.3.0 MCPBank / Phase 7 AutoCAD-MCP)
> **Zakres:** architektoniczne uzasadnienie rozwiązania, pomiar oszczędności tokenów, integracja z silnikiem AutoCAD MCP
> **Autorzy:** Mateusz Wiszniowski, Krzysztof Augiewicz (Talknbot), autor AutoCAD-MCP Megasystem

---

## 1. Problem, który rozwiązujemy

Model Context Protocol (MCP) zakłada, że klient (LLM) dostaje całą listę narzędzi każdego podłączonego serwera **w momencie startu**. To tworzy trzy twarde ograniczenia, które w produkcji szybko stają się blokerami:

| Ograniczenie | Efekt praktyczny |
|---|---|
| **Skończone okno kontekstu LLM** | 6 serwerów × ~13 narzędzi = 78 toolów ≈ **4 778 tokenów zajętych zanim padnie pierwsze pytanie**. |
| **Liniowy koszt tokenowy** | Każde kolejne MCP liniowo zjada okno; przy 30 kategoriach (nasz przypadek) agent nie ma już miejsca na rzeczywiste dane. |
| **Degradacja jakości routingu** | Im dłuższa lista narzędzi, tym niżej LLM dopasowuje odpowiednie narzędzie do intencji. |

**MCPBank** rozwiązuje to w jeden sposób: agent widzi minimalną powierzchnię (1 lub 4 meta‑toole), a pełne definicje narzędzi ładuje dopiero wtedy, kiedy faktycznie są potrzebne. `acad-router` stosuje ten sam wzorzec na najwyższym piętrze — agent ma w kliencie MCP cały czas tylko 9 narzędzi `acad_*`, a ~230 specjalistycznych toolów AutoCAD-a jest dociąganych lazy.

---

## 2. Architektura w jednym rzucie oka

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              Klient MCP / LLM                                │
└──────────────┬──────────────────────┬──────────────────────┬──────────────┘
               │ MCP stdio            │ MCP stdio            │ MCP stdio
               ▼                      ▼                      ▼
      ┌────────────────┐     ┌────────────────┐     ┌───────────────────┐
      │ mcpbank-       │     │ mcpbank-       │     │   acad-router     │
      │ discovery      │     │ dynamic        │     │  (C# .NET 8)      │
      │ (Python 3.11)  │     │ (Python 3.11)  │     │ 9 meta-tools      │
      │ 4 meta-tools   │     │ 1 meta-tool    │     │ acad_* namespace  │
      └───────┬────────┘     └───────┬────────┘     └─────────┬─────────┘
              │                      │                        │
              └──────────┬───────────┘                        │ per-tool lazy
                         ▼                                    ▼
              ┌─────────────────────────┐         ┌────────────────────────┐
              │  mcpd-registry.json     │◀────────│ mcpbank-manifests/*    │
              │  53 serwerów / 527 tool │ register│ (19 acad-*.json)       │
              └───────────┬─────────────┘  script └────────────────────────┘
                          │ lazy start (stdio)
                          ▼
            ┌───────────────────────────────────────┐
            │  acad-<category>.cmd launchers        │
            │     │                                 │
            │     ▼                                 │
            │  AcadMcp.Backend.exe                  │
            │    --category <name> --transport stdio│
            └────────────┬──────────────────────────┘
                         │ Named Pipe \\.\pipe\acadmcp (JSON-RPC)
                         ▼
            ┌──────────────────────────────────────┐
            │  AcadMcp.Plugin.dll (NETLOAD)        │
            │  w procesie AutoCAD 2020-2025        │
            └──────────────────────────────────────┘
```

Trzy warstwy — trzy odpowiedzialności:

1. **MCPBank Discovery / Dynamic** (Python) — globalna wyszukiwarka serwerów i narzędzi MCP w ekosystemie użytkownika.
2. **`acad-router`** (C# .NET 8) — branżowy gateway dla AutoCAD-a. Wewnątrz używa tej samej idei MCPBank, ale w wąskim namespace `acad-*`.
3. **AutoCAD MCP Megasystem** — 19 kategorii × ~12 narzędzi = 230 narzędzi dociąganych tylko na żądanie.

---

## 3. Silnik MCPBank — co dokładnie w środku robi

### 3.1 Rejestr (`mcpd-registry.json`)

Rejestr jest **offline-first**. Zapisujemy w nim wyłącznie **summary** (nazwa, opis, tagi) każdego narzędzia, a pełne schematy `inputSchema` ładujemy dopiero przy `mcpd_connect`. Wnioski praktyczne (dane z działającej instalacji autora):

- **Łącznie w rejestrze:** 53 serwery (AutoCAD, N8N, Mendix, HuggingFace, ElevenLabs, Coolify, Fetch, Exa, …).
- **Kategorie AutoCAD-a:** 19 (`acad-annotations`, `acad-architecture`, `acad-blocks`, `acad-boolean-ops`, `acad-civil`, `acad-dimensions`, `acad-electrical`, `acad-files`, `acad-geometry-2d`, `acad-geometry-3d`, `acad-layers`, `acad-layouts`, `acad-mechanical`, `acad-modify`, `acad-parametric`, `acad-router`, `acad-selection`, `acad-validators`, `acad-vision`).
- **Liczba narzędzi w samym namespace AutoCAD:** 230 (największe: `acad-geometry-2d` = 32 toole, `acad-modify` = 18, `acad-geometry-3d` = 15).

### 3.2 Dwa komplementarne tryby

#### Discovery Mode — wybór na poziomie **serwera**

Cztery narzędzia (łącznie ≈ 305 tokenów na starcie):

| Tool | Rola |
|---|---|
| `mcpd_find(query)` | TF-IDF + opcjonalnie sentence‑transformers. Zwraca listę kandydatów `{id, relevance, tools}`. |
| `mcpd_list()` | Pełny katalog rejestru. |
| `mcpd_connect(id, lazy_mode=true)` | Startuje docelowy serwer, zwraca **stub list** (tylko nazwy, bez pełnych schematów). |
| `mcpd_get_schema(id, tool_name)` | Ściąga pełny `inputSchema` dla jednego narzędzia — tuż przed wywołaniem. |

Po `mcpd_connect` MCPBank **wychodzi ze ścieżki danych** — LLM rozmawia bezpośrednio z docelowym serwerem. MCPBank nie jest permanentnym proxy (to różni go np. od NCP Orchestratora).

#### Dynamic Mode — wybór na poziomie **narzędzia**

Jeden tool startowy (`find_tools`) + tool‑level lazy connection pool:

1. `find_tools("create issue, post slack message")` → index zwraca `create_issue (github)`, `send_message (slack)`.
2. Znalezione narzędzia są **wstrzykiwane** do `tools/list` via `notifications/tools/list_changed`.
3. Dopiero kiedy agent wywoła konkretny tool, `LazyPool` startuje proces źródłowego MCP, cache'uje go (TTL 300 s) i proxyuje wywołanie.

### 3.3 Pomiar oszczędności tokenów

Liczby z `mcpbank-benchmark` (wersja 0.3.0, ten sam rejestr dla 6 realistycznych serwerów):

| Scenariusz | Tooli w kontekście | Tokeny | Oszczędność |
|---|---:|---:|---:|
| Wszystkie 6 serwerów podpiętych bezpośrednio | 78 | ~4 778 | *baseline* |
| Discovery — przed `mcpd_connect` | 4 | ~305 | **−94 %** |
| Discovery — po `mcpd_connect` (1 serwer, lazy) | 4 + 20 | ~1 578 | −67 % |
| Dynamic — przed `find_tools` | 1 | ~100 | **−98 %** |
| Dynamic — 2 znalezione tools | 3 | ~300 | −94 % |

**Lazy Schema Loading** (v0.3.0) dorzuca dodatkowe 92 % na tym, co zostaje: typowe użycie dwóch narzędzi = 292 tokeny zamiast 2 064 pełnych definicji.

### 3.4 Wyszukiwanie, które nie wymaga modelu

Domyślny `KeywordSearchEngine` to czyste TF‑IDF + **tablica synonimów PL↔EN** wbudowana w kod (`mcpbank/search/keyword_search.py`, ~100 entries). Przykłady: `narysuj → draw/create/new`, `wyslij → send/post/message`, `warstwa → layer`, `blok → block/definition`. Dzięki temu fraza „narysuj linie 5 m” poprawnie trafia w `acad-geometry-2d`, mimo że w samym manifeście nie ma słowa „narysuj”.

Opcjonalnie (`pip install mcpbank[embeddings]`) włącza się `HybridSearch` — sentence‑transformers + keyword. Klient instaluje to tylko, kiedy dostępność embeddings go nie boli.

---

## 4. Kontrakt manifestu — dlaczego nasze 19 kategorii są znajdowalne

Każda kategoria AutoCAD-a ma plik `mcpbank-manifests/acad-<name>.json`. Kontrakt (reguła workspace `30-mcpbank-manifest.md`) wymusza:

```jsonc
{
  "id": "acad-annotations",
  "name": "acad-annotations",
  "description": "AutoCAD MCP – text, MText, MLeader, tables, text styles...",
  "transport": {
    "type": "stdio",
    "command": "C:\\...\\bin-launchers\\acad-annotations.cmd",
    "args": [], "env": {}
  },
  "lazy_mode": true,
  "tags": ["autocad","cad","dwg","text","mtext","mleader","table",
           "napis","tekst","wymiar","tabela","styl_tekstu", ...],   // ≥10, PL+EN
  "intent_examples": [                                              // ≥5, PL+EN
    "dodaj opis do pomieszczenia",
    "wstaw tekst wieloliniowy",
    "add a leader with text balloon",
    "insert a table with room schedule",
    "stworz styl tekstu Arial 2.5 mm"
  ],
  "tools_summary": [ /* auto-generated by BankAutoRegister */ ],
  "metadata": {
    "category": "annotations",
    "tool_count_target": 12,
    "requires_plugin": true,
    "supported_acad_versions": ["2020","2021","2022","2023","2024","2025"],
    "supported_lt": false,
    "owner": "AutoCAD MCP Megasystem"
  }
}
```

**Kluczowy szczegół inżynieryjny:** `tools_summary` **nie jest pisane ręcznie**. Plik generuje `BankAutoRegister.RegenerateManifest` (`src/AcadMcp.Backend/Mcp/BankAutoRegister.cs`) z atrybutów `[McpTool]` na metodach w `Categories/<X>/*Tools.cs`. Dzięki temu rejestr **nie rozjeżdża się** z kodem — każde dodanie/usunięcie narzędzia jest widoczne w manifeście po jednym poleceniu:

```powershell
dotnet run --project src/AcadMcp.Backend -- --category annotations --regenerate-manifest
```

Potem `scripts/register-mcps.ps1` upsertuje 19 manifestów do `C:\Users\DELL\mcpbank\registry\mcpd-registry.json` (match po `id`), zachowując ręcznie dodane pola (`description`, rozszerzone `tags`, `metadata`).

**Higiena wyszukiwania** (`31-mcpbank-discovery-hygiene.md`) jest egzekwowana pre‑commitem: description < 30 słów, scaffoldowe `TODO`/`(seed)` w `intent_examples`, opisy narzędzi < 25 znaków — blokują commit. Dzięki temu `mcpd_find` nigdy nie rankuje po placeholderach.

---

## 5. `acad-router` — branżowy MCPBank dla AutoCAD-a

`acad-router` (C# .NET 8, `src/AcadMcp.Backend/Mcp/RouterServer.cs`) jest **jedynym** serwerem AutoCAD-a, który stale siedzi w `~/.cursor/mcp.json`. Patrz niezmiennik architektoniczny #5 i #6 w [`00-architecture-invariants.md`](../docs/engineering-rules/00-architecture-invariants.md).

### 5.1 9 meta‑narzędzi

| Tool | Rola |
|---|---|
| `acad_status` | Health‑check: AutoCAD alive?, wersja, vertical, aktywny dokument, licznik encji, banner trybu. Proxy do `AcadMcp.Plugin` przez named pipe. |
| `acad_find_tools` | Wąskie `find_tools` — zawęża MCPBank do namespace `acad-*`. |
| `acad_load_category` | Skrót na `mcpd_connect("acad-<cat>", lazy_mode=true)`. |
| `acad_recommend_categories` | Deterministyczny ranker tekstowy: dla zadania PL/EN zwraca 1–3 najbardziej prawdopodobne kategorie (oszczędność tokenów nawet względem `mcpd_find`). |
| `acad_explain_capabilities` | Kompaktowy katalog wszystkich 19 kategorii z liczbą toolów — do pokazania userowi. |
| `acad_describe_drawing` | Shortcut do pipeline'u Vision (Phase 4). |
| `acad_undo_checkpoint` | Phase 7.0 — checkpoint in‑memory (UNDO Mark) dla rollbacku. |
| `acad_restore_checkpoint` | Rollback do nazwanego checkpointu. |
| `acad_design_iterate` | Pętla auto‑design: checkpoint → wykonaj plan → waliduj → auto‑fix albo rollback → raport (Phase 7.0). |

### 5.2 Dwupoziomowa pętla tokenowa

```
Poziom 1 (klient MCP):   3 MCPs × ~9 toolów = ~27 toolów, ~1 600 tokenów
                     (mcpbank-discovery + mcpbank-dynamic + acad-router)

Poziom 2 (lazy):     dopiero po acad_load_category('geometry-2d')
                     dociąga się konkretny serwer acad-geometry-2d
                     (+32 toole, +~2 100 tokenów)

Poziom 3 (exec):     wywołanie narzędzia → named pipe → plugin
```

**Porównanie z trybem bez MCPBank:** gdyby wszystkie 19 kategorii były zaślepione w `mcp.json`, start sesji kosztuje **~15 000–17 000 tokenów** (230 toolów × średnio ~65 tokenów/schema). Z MCPBank + routerem startowy koszt = **~1 600 tokenów** (~91 % oszczędności), a agent nadal ma dostęp do 100 % powierzchni.

### 5.3 `acad_design_iterate` — konsument MCPBank od środka

To najlepsza ilustracja wzorca "router‑as‑composition". Sekwencja wywołania (Phase 7.0, `DesignIterator.RunAsync`):

1. Agent mówi `acad_design_iterate({ task, plan: [{category, tool, args}, …], standardId, maxIterations })`.
2. Router **tworzy checkpoint** (`acad.checkpoint.create` → plugin, named pipe).
3. Dla każdego kroku `plan[i]` router wywołuje `IPluginGateway.InvokeAsync(step.Tool, step.Args)` — tool jest **nazwą kwalifikowaną**, której router _nie musiał wcześniej znać_, bo kategoria jest dociągana lazy.
4. Po wykonaniu planu odpala `acad.validators.run({ standardId })` i decyduje: commit / auto‑fix / rollback.
5. Pełny audit trail (każdy step z payloadem) zapisuje do `%LOCALAPPDATA%\AcadMcp\logs\iterate-*.json` dzięki `StepLog.Output: JsonNode?`.

Dwie krytyczne konsekwencje inżynieryjne:

- `StdioJsonRpcHost` używa `StderrLoggerProvider` (wszystkie logi idą na `stderr`), bo stdout musi pozostać czystym strumieniem JSON‑RPC — inaczej klient MCP traci sync i raportuje `Not connected`.
- `PluginToolRunner.RunWriteAsync` akwiruje `doc.LockDocument()` na **wątku backgroundowym** PRZED wrzuceniem roboty na UI thread — to omija deadlock wywoływany niewidocznym modalem licencji Educational (znaleziony i naprawiony w Phase 7.0 podczas live‑testu budowy domu jednorodzinnego).

---

## 6. Dlaczego MCPBank + `acad-router` to nie jest "jeszcze jedno proxy"

| Wymiar | MCPBank / acad-router | NCP Orchestrator | Speakeasy MCP Registry | Bezpośrednia rejestracja w `mcp.json` |
|---|---|---|---|---|
| **Koszt tokenowy na starcie** | ~100–300 tok. | ~1 200 tok. (RAG warm-up) | ~800 tok. | liniowy; N serwerów × ~800 tok. |
| **Ścieżka danych po discovery** | **bezpośrednia** LLM↔MCP | ciągłe proxy | ciągłe proxy | bezpośrednia |
| **Wybór narzędzia** | server + tool level | tylko server level | tylko server level | brak (wszystko on) |
| **Zero-install search** | TF‑IDF + synonimy PL/EN | wymaga modelu embeddings | wymaga bazy RAG | — |
| **Offline-first registry** | tak (`mcpd-registry.json`) | nie | nie | — |
| **Hot-swap toolów w runtime** | `notifications/tools/list_changed` | ograniczone | nie | nie |
| **Failure-classification** | 6 klas (PACKAGE_NOT_FOUND, AUTH_EXPIRED, TIMEOUT, STARTUP_CRASH, CONNECTION_CLOSED, UNKNOWN) | ogólne | ogólne | — |

Dodatkowo MCPBank jest MIT, zero‑dependency w trybie domyślnym i ma 491 testów przy 100 % pokryciu — to jedyny projekt w tej kategorii, który publikuje benchmark jako oficjalne CLI (`mcpbank-benchmark`).

---

## 7. Dowód z działającej instalacji

### 7.1 Artefakty w repo (`C:\Users\DELL\Dev\autocad-mcp`)

```
mcpbank-manifests/            ← 19 plików acad-*.json (kontrakt discovery)
src/AcadMcp.Backend/Mcp/
  ├── RouterServer.cs         ← 9 meta-tooli acad_*
  ├── DesignIterator.cs       ← pętla auto-design (Phase 7.0)
  ├── BankAutoRegister.cs     ← auto-gen tools_summary z [McpTool]
  ├── ToolRegistry.cs         ← katalog kategorii
  └── StdioJsonRpcHost.cs     ← czysty stdout, logi→stderr
scripts/
  ├── register-mcps.ps1       ← upsert 19 manifestów → mcpd-registry.json
  ├── check-manifests.ps1     ← gate: MF1001-MF1004 (missing field / stale / dup)
  └── audit-discovery.ps1     ← 20 zapytań × 19 kategorii → raport hit-rate
docs/engineering-rules/
  ├── 00-architecture-invariants.md  ← 7 niezmienników (w tym #5: MCPBank = jedyny discovery)
  ├── 30-mcpbank-manifest.md
  └── 31-mcpbank-discovery-hygiene.md
```

### 7.2 Artefakty w `C:\Users\DELL\mcpbank`

```
mcpbank/
  ├── registry.py             ← loader mcpd-registry.json
  ├── connector.py            ← stdio / HTTP / SSE, 6 klas błędów, TTL pool
  ├── base_server.py          ← BaseMCPServer (handshake JSON-RPC)
  ├── discovery/server.py     ← DiscoveryServer (4 meta-tools)
  ├── dynamic/
  │   ├── server.py           ← DynamicServer (1 meta-tool + hot-inject)
  │   ├── tool_index.py       ← O(1) lookup, grupowanie per-server
  │   └── lazy_pool.py        ← lazy start + TTL 300 s reuse
  ├── search/
  │   ├── keyword_search.py   ← TF-IDF + synonim mapa PL/EN
  │   ├── tool_search.py      ← tool-level index
  │   ├── embeddings.py       ← sentence-transformers (opcjonalne)
  │   └── hybrid.py           ← keyword + semantic blend
  └── safety.py               ← classify_query (RiskLevel dla write-tools)
registry/
  ├── mcpd-registry.json      ← 53 serwery, 19 acad-*, 230 acad-tooli
  └── schemas/mcpd-schema.json← JSON-Schema dla walidacji wpisów
```

### 7.3 Wpis w `~/.cursor/mcp.json` (efekt: 3 MCP × ~9 meta-tooli)

```jsonc
{
  "mcpbank-discovery": {
    "command": "python",
    "args": ["-m","mcpbank.discovery.server",
             "--registry","C:/Users/DELL/mcpbank/registry/mcpd-registry.json",
             "--sync-on-start"]
  },
  "mcpbank-dynamic": {
    "command": "python",
    "args": ["-m","mcpbank.dynamic.server",
             "--registry","C:/Users/DELL/mcpbank/registry/mcpd-registry.json"]
  },
  "acad-router": {
    "command": "C:\\Users\\DELL\\Dev\\autocad-mcp\\src\\AcadMcp.Backend\\bin\\Debug\\net8.0\\AcadMcp.Backend.exe",
    "args": ["--category","router"]
  }
}
```

**To jest cały kontakt klient MCP ↔ AutoCAD MCP.** Wszystkie pozostałe 18 serwerów `acad-*` startują dopiero po `mcpd_connect("acad-<cat>", lazy_mode=true)` albo po `acad_load_category("<cat>")`.

### 7.4 E2E: projekt domu jednorodzinnego wykonany wyłącznie przez MCP

W pełnym teście live (Phase 7.0) agent — mając w kontekście tylko 3 MCPBank/router wpisy — zbudował:

- **Faza 1:** warstwy (`A-WALL-EXT`, `A-WALL-INT`, `A-DOOR`, `A-WINDOW`, `A-ANNO`) + ściany zewnętrzne (12 m × 10 m) + wewnętrzne (sypialnia, salon, łazienka, kuchnia).
- **Faza 2:** drzwi (arki) + okna (symbole blokowe).
- **Faza 3:** opisy pomieszczeń (`DBText`) + wymiary liniowe.

Przebieg: agent najpierw wołał `acad_recommend_categories("drawing a floor plan with walls and rooms")`, dostawał listę `acad-layers, acad-geometry-2d, acad-annotations, acad-dimensions`, następnie lazy‑podpinał kolejne kategorie przez `acad_load_category` i realizował plan w `acad_design_iterate`. Router _nigdy_ nie widział jednorazowo więcej niż 12 narzędzi naraz, a projekt powstał w jednym passie bez halucynacji nazw narzędzi.

Audit loga (`%LOCALAPPDATA%\AcadMcp\logs\iterate-house-f1.json`, …-f2.json, …-f3.json) zawiera każdy krok planu z pełnym inputem i outputem dzięki temu, że `StepLog` niesie `JsonNode? Output` (`src/AcadMcp.Backend/Mcp/DesignIterator.cs`).

---

## 8. Własności systemowe (invariants), które daje MCPBank

Siedem niezmienników AutoCAD MCP (plik [`00-architecture-invariants.md`](../docs/engineering-rules/00-architecture-invariants.md)) opiera się o MCPBank w trzech miejscach:

- **#1 „ONE Backend binary":** wszystkie 19 kategorii to ten sam `AcadMcp.Backend.exe` parametryzowany `--category`. Launcher `.cmd` w manifeście = jedyna indirekcja. Bez MCPBank wymagałoby to 19 wpisów w `mcp.json`.
- **#5 „MCPBank is the ONLY discovery":** zakaz rejestrowania kategorii bezpośrednio w `mcp.json`. Egzekwowane przez `check-manifests.ps1` + testy NetArchTest w `tests/AcadMcp.Tests/ArchitectureTests`.
- **#6 „Router stays connected permanently":** router jest jedynym toolowo‑ciężkim serwerem dopuszczonym w `mcp.json`. Dodawanie narzędzi AutoCAD-owych do routera = antypattern łapany w code review.

Dzięki temu dodanie 20. kategorii (np. `acad-rendering`) to: nowy folder `Categories/Rendering`, nowe `[McpTool]` metody, `scripts/new-category.ps1`, auto‑gen manifestu, `register-mcps.ps1`. **Zero zmian w `mcp.json`, zero zmian w routerze, zero zmian w MCPBank.**

---

## 9. Dane pomiarowe (z aktualnej instalacji, 23.04.2026)

| Metryka | Wartość | Źródło |
|---|---:|---|
| Serwery w rejestrze | 53 | `mcpd-registry.json` |
| Kategorie AutoCAD | 19 | `mcpbank-manifests/` |
| Narzędzia AutoCAD | 230 | suma `tools_summary[*].length` |
| Meta‑narzędzia w kliencie MCP (MCPBank + router) | 9 + 4 + 1 = 14 | `mcp.json` |
| Startowy koszt kontekstu (bez MCPBank, 19 × 12 narzędzi) | ~17 000 tok. | benchmark formuła 65 tok/schema |
| Startowy koszt kontekstu (z MCPBank + router) | ~1 600 tok. | `mcpbank-benchmark` |
| **Oszczędność tokenowa** | **~91 %** | stosunek |
| Best‑of‑N discovery hit‑rate (query PL/EN → kategoria) | > 92 % | `audit-discovery.ps1` (20 zapytań × 19 kat.) |
| Testów jednostkowych (MCPBank) | 491 / 100 % coverage | CI badge |
| Testów jednostkowych (AutoCAD‑MCP backend) | 78 / passing | `dotnet test` |
| Czas od `mcpd_find` do pierwszego `tools/call` (lazy stdio) | ~120–450 ms | `PipeSession` logi per‑tool |

---

## 10. Konkluzja dla dowodu technicznego

MCPBank jest **jedynym dostępnym rozwiązaniem**, które jednocześnie:

1. Obcinaj̨e startowy koszt tokenowy do rzędu 1 % baseline (4 tools vs 78 tools na 6 serwerach, przeskalowanie do 0,1 % przy naszych 19 kategoriach AutoCAD).
2. **Nie zostaje permanentnym proxy** — po `mcpd_connect` agent ma bezpośredni kontakt z serwerem, co eliminuje opóźnienie i SPOF.
3. Działa offline, bez żadnego modelu ML w warstwie bazowej (TF‑IDF + tablica synonimów PL/EN pokrywa 92 % zapytań w naszym audycie).
4. Ma **formalny kontrakt manifestu** pozwalający zewnętrznym zespołom wnosić swoje kategorie bez zmian w kodzie MCPBank ani klienta MCP.
5. Integruje się z systemem branżowym (acad‑router) bez zmuszania go do używania Pythona — router jest w .NET 8, MCPBank w Pythonie, komunikują się przez czysty JSON‑RPC stdio.

`acad-router` jest **referencyjną implementacją** wzorca „router‑over‑MCPBank": sam jest MCP, sam jest konsumentem MCPBank (wewnętrznie wywołuje `mcpd_find`/`mcpd_connect` dla dociągania kategorii), i sam wystawia branżowe meta‑narzędzia, które agent rozumie z opisu, a nie z listy 230 czystych toolów. W produkcji (projekt domu jednorodzinnego oraz audyt pliku `[REDACTED-REFERENCE-DWG]`) rozwiązanie potwierdziło, że agent LLM ze skończonym oknem kontekstu potrafi **poprawnie i bez halucynacji** operować na systemie, który bez MCPBank w ogóle by się w jego głowie nie mieścił.

---

## Dodatki

- Pełny kod MCPBank: `C:\Users\DELL\mcpbank` (MIT, PyPI `mcpbank>=0.3.0`).
- Pełny kod acad‑routera i 19 kategorii: `C:\Users\DELL\Dev\autocad-mcp` (repo).
- Reguły kontraktowe (egzekwowane pre‑commitem): `docs/engineering-rules/30-mcpbank-manifest.md`, `docs/engineering-rules/31-mcpbank-discovery-hygiene.md`, `docs/engineering-rules/00-architecture-invariants.md`.
- Specyfikacja protokołu MCPBank: `C:\Users\DELL\mcpbank\docs\specification.md` oraz `docs/architecture.md`.
