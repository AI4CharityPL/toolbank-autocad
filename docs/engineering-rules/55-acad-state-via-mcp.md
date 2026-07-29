# Stan AutoCADa weryfikujemy WYŁĄCZNIE przez MCP

AutoCAD state (alive, version, active document, layer, entities) MUST be checked via MCP (acad_status). NEVER via Get-Process, tasklist, ps or terminal.

Cały ten projekt istnieje po to, by agent nie musiał domyślać się stanu AutoCADa z systemu operacyjnego. Jeżeli w trakcie pracy potrzebujesz się dowiedzieć:

- czy AutoCAD jest uruchomiony,
- jaka jest jego wersja / vertical (AutoCAD, Architecture, Mechanical, Civil 3D),
- który dokument (`.dwg`) jest aktywny,
- jaka jest aktywna warstwa,
- ile jest encji w modelu,
- czy plugin jest załadowany (pipe `\\.\pipe\acadmcp`),

**zawsze** wołasz `acad_status` z `user-acad-router` (Invariant #6 w `00-architecture-invariants.md`).

## Dlaczego nie Get-Process / tasklist / ps

1. `Get-Process -Name acad` pokazuje obecność procesu, ale NIE mówi: czy plugin `AcadMcp.Plugin.dll` jest NETLOAD'ed, czy pipe żyje, ani który dokument jest aktywny.
2. Licencje Educational mają niewidoczne modale (`acad.exe` żyje, ale UI thread jest zablokowany) – proces widoczny, CAD w rzeczywistości martwy. Tylko `acad_status` (round-trip przez pipe) to wykrywa.
3. Trzy użytkownicy w tej samej sesji Windows mogą mieć kilka `acad.exe` – terminalowy `Get-Process` nie powie, który jest naszym targetem; pipe mówi to jednoznacznie.
4. MCP jest jedynym oficjalnym kontraktem systemu (`00-architecture-invariants.md` Invariant #3: Named Pipe = ONLY bridge) – każde inne źródło prawdy dryfuje.

## Poprawnie

```jsonc
// wywołanie meta-toola
CallMcpTool server="user-acad-router" toolName="acad_status" arguments={}
// zwraca:
{
  "alive": true,
  "acadProductName": "AutoCAD",
  "acadVersion": "25.0.0.0",
  "documentName": "C:\\...\\[REDACTED-REFERENCE-DWG]",
  "activeLayer": "0",
  "entityCount": 231019,
  "isLT": false,
  "vertical": null,
  "modeBanner": "full"
}
```

Dopiero to jest dowodem, że agent może zacząć jakąkolwiek operację rysunkową.

## Niepoprawnie (blokowane przez review)

```powershell
Get-Process -Name "acad","accoreconsole","AcadMcp.Backend"   # ❌ nie mówi nic o pipe
tasklist | findstr acad                                       # ❌ to samo
ps aux | grep acad                                            # ❌ + zła platforma
```

Jeżeli agent uzyje jednego z powyzszych, traktuj to jako łamanie Invariant #3 i wymuś poprawkę na `acad_status`.

## Kiedy `acad_status` zwraca `alive: false` / błąd

Dopiero wtedy wolno:
1. Uruchomić `scripts/deploy-plugin.ps1 -Kill` (gdy podejrzewasz zawieszonego `acad.exe` z niewidocznym modalem).
2. Poprosić użytkownika o ręczny restart AutoCADa.
3. Sprawdzić logi `%LOCALAPPDATA%\AcadMcp\logs\pipe-*.log`.

## Rozszerzenia tej zasady

Tak samo traktujemy inne stany rysunku — zawsze pierwszy krok to MCP, nie terminal:

| Chcę wiedzieć... | Tool |
|---|---|
| Czy rysunek ma jakieś encje? / ile? | `acad_status` lub `acad.validators.doc_summary` |
| Jakie są warstwy / czy istnieje `A-WALL-EXT`? | `acad.layers.list_layers` |
| Jakie są bloki / atrybuty / dynamic? | `acad.blocks.list_blocks` |
| Jakie są layouty? | `acad.layouts.list_layouts` |
| Czy konkretna encja istnieje po uchwycie? | `acad.selection.get_entity_info` |

Wszystko to jest w namespace `acad-*` dostępnym przez `acad_load_category` / MCPBank — NIGDY z poziomu shella.
