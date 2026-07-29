# Persona: `senior-architect-reviewer`

**Endpoint:** `POST /v1/architect-review`
**Rule:** [60-architectural-fidelity](../../../.cursor/rules/60-architectural-fidelity.mdc)
**Phase:** D11

This persona is the **exit gate** for every floor plan that ships from this
repository (Hospital2026, future clinic / office / residential projects).
It grades each drawing on the canonical **17-criterion rubric** defined by
rule 60 §1 and returns a strict `{ 0 / 0.5 / 1 }` score per criterion, a
weighted total out of 17.0, and a verdict tier.

Unlike the lighter `architect-reviewer` persona (five free-form headings
for mid-iteration triage), this persona is the contract used by
`acad_design_iterate` when `qualityTarget >= 15.0`. Callers MUST block
export / PDF / tender packaging whenever `score < 15`.

## Request (application/json)

```json
{
  "image": { "path": "C:\\project\\plans\\A0-001.png" },
  "language": "en",
  "brief": "70-bed oncology ward, 4x OR, 2x PACU, ..."
}
```

- `image` - absolute path or base64 data URL (same contract as every other
  Vision endpoint).
- `language` - `"en"` or `"pl"` (default `"en"`).
- `brief` - optional plain-text programme / compliance cues. When supplied
  it is appended to the persona system prompt as "Project brief:" so
  criterion 17 (`room-program`) can score the `± 10%` area requirement
  against reality.
- `max_tokens` - forwarded to the underlying LLM (default 1600).
- `provider` - `"anthropic"`, `"openai"`, `"google"` or `"auto"` (default).

### Recommended provider: Google Gemini 3.1 Pro (April 2026)

The persona is tuned for, and the default production configuration uses,
**`gemini-3.1-pro-preview`** (Google, released 2026-02-19). As of April
2026 it leads every multimodal benchmark that matters for floor-plan
review - MMMU-Pro (75.1%), Video-MME (78.2%), DocVQA (95.7%) - and its
2M-token context easily holds a 1568 px raster plus the full 17-criterion
system prompt with thinking-mode reasoning tokens.

- Set `GOOGLE_API_KEY` (or `GEMINI_API_KEY` - the SDK accepts either).
- Optional: `ACADMCP_GOOGLE_MODEL` to pin a different preview ID (e.g.
  `gemini-3.1-pro-preview-customtools` if you route tool calls through
  the persona).
- Optional: `ACADMCP_GOOGLE_THINKING` in `{low, medium, high, max}` -
  defaults to `high` because the 17-criterion scorecard is an output of
  reasoning, not skim-read perception.

Cost envelope for budgeting: roughly $0.025 per architect-review call at
`gemini-3.1-pro-preview` pricing ($2 / 1M input, $12 / 1M output), so
every 1 USD of Google AI credit buys ~40 reviews.

## Response

```json
{
  "score": 15.5,
  "verdict": "executive-with-remark",
  "criteria": [
    { "id": 1,  "label": "hatching",          "score": 1.0,
      "note": "all six material presets visible, boundaries continuous." },
    { "id": 2,  "label": "furniture",         "score": 0.5,
      "note": "OR missing procedure lights; fix: acad.furniture.populate_room preset=or." },
    ...17 rows in canonical order...
  ],
  "fatal_gaps": [2, 6, 15],
  "threshold_note": "score 14..15 / 17 - executive-grade with remark; ...",
  "raw_text": "...",
  "provider": "anthropic",
  "model": "claude-3-5-sonnet-20241022"
}
```

Verdict tiers (rule 60 threshold policy):

| Score   | Verdict                   | Meaning                                     |
|--------:|:--------------------------|:--------------------------------------------|
| < 10    | `concept-sketch`          | Do NOT export. Rerun generators.            |
| 10..13  | `technical-study`         | Internal review only.                       |
| 14..15  | `executive-with-remark`   | Sign-off allowed with remark.               |
| 16..17  | `full-wykonawczy`         | Clear for export / tender / pozwolenie.     |

## Contract invariants

1. The response **always** has exactly 17 `criteria` rows in canonical
   order. If the underlying LLM omits one, the server emits `score=0.0`
   with the note `"persona did not score this criterion"` (rule 60 §2 -
   never silently drop a row).
2. Scores are snapped to the nearest half (0.0 / 0.5 / 1.0). Raw LLM
   outputs outside `[0, 1]` are clamped.
3. For every row with `score < 1.0`, the LLM is instructed to cite a fix
   tool in the form `fix: acad.<category>.<tool>` drawn from rule 60 §3.
4. `fatal_gaps` lists every criterion ID with `score < 1.0` (sorted
   ascending). Callers treat `len(fatal_gaps)` as the minimum number of
   generator re-runs still required.
5. The rubric contract lives in `acadmcp_vision.schemas.ARCHITECT_REVIEW_CRITERIA`
   as a module-level constant. It is **authoritative** - do not reorder,
   rename, or add an 18th row without first updating rule 60 §5 and the
   planning document `docs/PLAN-PROFESSIONAL-UPGRADE-2026.md §2`.

## Relation to `/v1/describe-image`

`senior-architect-reviewer` is also registered as a valid `persona=` value
on `/v1/describe-image`. In that mode the LLM is steered with the same
system prompt but the server does **not** parse / score / threshold - the
raw text is returned verbatim. Use the dedicated `/v1/architect-review`
endpoint when you need the structured scorecard.
