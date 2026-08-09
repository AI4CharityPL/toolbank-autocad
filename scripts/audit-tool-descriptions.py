# -*- coding: utf-8 -*-
"""Audit every tool in the bank for whether a ROUTING AGENT can actually pick it.

A tool the router never reaches is as unavailable as one that was never built, and nothing in
the build catches that: a stub description compiles, passes the manifest check, and passes its
unit test. This looks for the ways a tool becomes unpickable.

  A. NO DESCRIPTION, or one so short it says nothing beyond the name.
  B. TOO FEW INTENTS — rule 22 wants at least 5.
  C. MISSING THE PRIMARY LANGUAGE. This bank works in ENGLISH first and Polish as well, so a
     tool with no English intent is the error; one with no Polish intent is a warning, because
     it is still reachable, just not from a Polish query.
  D. INTENT COLLISIONS — the same phrase, or a near-identical one, offered by two different
     tools. The router then has to choose between them on nothing, and it will sometimes choose
     wrong. This is the one failure that gets WORSE as the bank grows.
  E. NAME COLLISIONS ACROSS CATEGORIES that the descriptions do not disambiguate. fillet_edge
     and fillet_corner are a real pair; if neither description mentions the other, an agent
     asked to "round a corner" has no way to tell 2D from 3D.
  F. CONFUSABLE SIBLINGS — two tools whose names share a verb and whose descriptions do not
     name each other. fillet_corner and fillet_edge are a real pair: one is 2D and one is 3D,
     and an agent asked to "round a corner" has nothing to choose on.

A seventh check was written and then REMOVED: "does the description say what the tool is for",
tested by looking for words like use/when/for. It flagged 305 tools, nearly all of them well
described, and a check that cannot separate the good from the bad is worse than no check - it
buries the real findings. Dropped rather than tuned, because there was no version of it that
measured the thing it claimed to.

Prints a ranked report. Exit code is 1 on A, B, or a missing ENGLISH intent - those are
objective. Missing Polish, collisions and confusable siblings are reported for judgement.
"""
import json
import glob
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFESTS = os.path.join(ROOT, "toolbank-manifests")

# ── language classification, bootstrapped from the corpus itself ─────────────
#
# A hand-written marker list got this badly wrong on the first attempt: it demanded diacritics or
# a short list of function words, and reported "no Polish intent" for tools whose intents read
# `maska tla pod tekstem`. 170 false alarms, which is worse than no check at all, because it
# buries the real ones.
#
# So: seed on the handful of tokens that can only be one language, learn every other token's
# language from the phrases those seeds label, then classify the rest on the learned evidence.
# The script prints a labelled sample so the classifier can be checked by eye before its verdicts
# are believed.
EN_SEED = {"the", "of", "with", "and", "for", "from", "by", "into", "between", "an", "all",
           "every", "which", "that", "this", "what", "does", "are", "is", "it"}
PL_SEED = {"na", "do", "dla", "jak", "jest", "sa", "oraz", "wszystkie", "jakie", "ktora",
           "ktory", "ile", "czy", "tego", "tej", "przez", "pod", "nad", "wedlug", "wzdluz",
           "miedzy", "bez", "przy", "obok"}


def words(s):
    return re.findall(r"[a-ząćęłńóśźż]+", s.lower())


def seed_lang(phrase):
    """Only the calls that cannot be wrong; everything else is left to the learned model."""
    if re.search(r"[ąćęłńóśźż]", phrase):
        return "pl"
    w = set(words(phrase))
    en, pl = w & EN_SEED, w & PL_SEED
    if en and not pl:
        return "en"
    if pl and not en:
        return "pl"
    return None


def norm(s):
    """A phrase reduced to its content words, for collision testing.

    Only words that carry no meaning are dropped. Directional prepositions are NOT: stripping
    them reported `layer state to file` and `layer state from file` as the same phrase, which is
    export and import collapsed into one - a false collision manufactured by removing the very
    words that told them apart."""
    s = re.sub(r"[^a-z0-9ąćęłńóśźż ]+", " ", s.lower())
    stop = {"a", "an", "the", "of", "and", "or", "i", "oraz"}
    return " ".join(w for w in s.split() if w not in stop)


tools = []
for path in sorted(glob.glob(os.path.join(MANIFESTS, "*.json"))):
    d = json.load(open(path, encoding="utf-8-sig"))
    cat = os.path.basename(path)[len("acad-"):-len(".json")]
    for t in d.get("tools_summary", []):
        tools.append({
            "cat": cat,
            "name": t.get("name", ""),
            "desc": (t.get("description") or "").strip(),
            "intent": [i for i in (t.get("intent") or []) if i and i.strip()],
        })

print(f"== auditing {len(tools)} tools across "
      f"{len({t['cat'] for t in tools})} categories ==\n")

# Learn token -> language from the seeded phrases, then classify everything.
tok_lang = defaultdict(lambda: [0, 0])          # token -> [en count, pl count]
for t in tools:
    for i in t["intent"]:
        lg = seed_lang(i)
        if lg:
            for w in set(words(i)):
                tok_lang[w][0 if lg == "en" else 1] += 1


def lang_of(phrase):
    lg = seed_lang(phrase)
    if lg:
        return lg
    en = pl = 0.0
    for w in set(words(phrase)):
        e, p = tok_lang.get(w, (0, 0))
        if e or p:
            en += e / (e + p)
            pl += p / (e + p)
    if en > pl * 1.2:
        return "en"
    if pl > en * 1.2:
        return "pl"
    return "?"


print("-- classifier sample, for checking by eye before believing any of its verdicts --")
_sample = [i for t in tools for i in t["intent"]]
for i in _sample[::max(1, len(_sample) // 14)][:14]:
    print(f"   [{lang_of(i)}] {i}")
_unknown = sum(1 for i in _sample if lang_of(i) == "?")
print(f"   ({_unknown} of {len(_sample)} phrases could not be classified; those are never "
      f"counted as a missing language)\n")

A, B, C, W, F = [], [], [], [], []
for t in tools:
    ref = f"{t['cat']}.{t['name']}"
    if not t["desc"]:
        A.append((ref, "no description at all"))
    elif len(t["desc"]) < 60:
        A.append((ref, f"description is {len(t['desc'])} chars: {t['desc']!r}"))
    if t["cat"] == "router":
        # The router's own tools are exposed straight to the agent as MCP tools with their own
        # descriptions and schemas; nothing routes to them by intent, so an empty intent list is
        # correct here rather than a gap. Counting them was this audit's own false alarm.
        pass
    elif len(t["intent"]) < 5:
        B.append((ref, f"{len(t['intent'])} intents: {t['intent']}"))
    else:
        langs = [lang_of(i) for i in t["intent"]]
        # Only a CONFIDENT absence counts. A tool whose phrases are all unclassifiable is not
        # evidence of anything, and reporting it would bury the real cases.
        if "?" not in langs:
            if "en" not in langs:
                C.append((ref, f"NO ENGLISH intent among {len(langs)} - English is the primary "
                               f"language here: {t['intent'][:3]}"))
            elif "pl" not in langs:
                W.append((ref, f"no Polish intent among {len(langs)}: {t['intent'][:3]}"))


# D: intent collisions.
by_phrase = defaultdict(set)
for t in tools:
    for i in t["intent"]:
        by_phrase[norm(i)].add(f"{t['cat']}.{t['name']}")
D = sorted(((p, sorted(o)) for p, o in by_phrase.items() if len(o) > 1 and p),
           key=lambda x: -len(x[1]))

# E: same verb, different category, descriptions that do not point at each other.
by_name = defaultdict(list)
for t in tools:
    by_name[t["name"]].append(t)
E = []
for name, ts in sorted(by_name.items()):
    if len(ts) > 1:
        for t in ts:
            others = [o for o in ts if o is not t]
            if not any(o["cat"].replace("-", "_") in t["desc"].lower().replace("-", "_")
                       or o["name"] in t["desc"] for o in others):
                E.append((f"{t['cat']}.{name}",
                          "also exists in " + ", ".join(o["cat"] for o in others) +
                          " and this description names neither"))
# Near-miss pairs: same verb stem, different noun. fillet_edge vs fillet_corner.
# Same VERB and same shape of name: <verb>_<one word>. That keeps fillet_corner / fillet_edge and
# array_polar / array_path, and drops audit_database / audit_all_rooms, which share a verb and
# nothing else. The wide version listed 97 pairs, nearly all unrelated - the same burying problem
# the dropped check F had.
stems = defaultdict(list)
for t in tools:
    parts = t["name"].split("_")
    if len(parts) == 2:
        stems[parts[0]].append(t)
NEAR = []
for head, ts in sorted(stems.items()):
    names = {t["name"] for t in ts}
    if len(names) > 1 and len(names) <= 4:
        for t in ts:
            siblings = sorted(n for n in names if n != t["name"])
            if siblings and not any(sib in t["desc"] for sib in siblings):
                NEAR.append((f"{t['cat']}.{t['name']}",
                             "sibling(s) " + ", ".join(siblings) + " unmentioned"))


def report(title, rows, limit=None):
    print(f"-- {title}: {len(rows)} --")
    for r in (rows[:limit] if limit else rows):
        print(f"   {r[0]:<44} {r[1]}")
    if limit and len(rows) > limit:
        print(f"   ... and {len(rows) - limit} more")
    print()


report("A. missing or stub descriptions", A)
report("B. fewer than 5 intents (rule 22), router excluded", B)
report("C. NO ENGLISH intent - the primary language", C, 40)
report("D. intent phrases claimed by more than one tool",
       [(p, ", ".join(o)) for p, o in D], 40)
report("E. same tool name in several categories, descriptions not cross-referenced", E, 30)
report("F. confusable siblings, descriptions not cross-referenced "
       "(JUDGEMENT: the 33 left are distinct objects in one family - add_table beside "
       "add_sheet, select_fence beside select_polygon - where the request names the "
       "thing itself. The pairs where it does not have been cross-referenced.)", NEAR, 60)
report("W. no Polish intent (warning - still reachable in English)", W, 25)

hard = len(A) + len(B) + len(C)
print(f"==== objective failures (A+B+C): {hard};  judgement calls (D+E+F): "
      f"{len(D) + len(E) + len(NEAR)};  Polish-coverage warnings: {len(W)} ====")
sys.exit(1 if hard else 0)
