#!/usr/bin/env python3
"""
Convert a NewRecruit / BattleScribe 10th-edition Necron *roster* export into the TombForge
catalogue seed (necron-catalogue-seed.json).

The export must contain every datasheet with all of its weapon loadout variations added (so that
every weapon profile and every option-group is present). Variants of the same datasheet are merged
by name; the mutually-exclusive options of a wargear group are reconstructed from the shared
`entryGroupId`.

Points options and Pantheon bindings are merged from the existing seed (a roster export only carries
the unit sizes that were actually added, and bindings are a TombForge concept the export lacks).

Derived fields (id, isMonster, maxCopies, leaderTargetIds, ...) are intentionally NOT written:
CatalogueSeedLoader.Enrich computes them at load time.

Usage:
	python tools/convert_newrecruit_to_seed.py
	python tools/convert_newrecruit_to_seed.py --input tools/necron-roster-export.json \
		--existing Warhammer40k.Api/Seed/necron-catalogue-seed.json \
		--output   Warhammer40k.Api/Seed/necron-catalogue-seed.json
"""

import argparse
import collections
import json
import re

WEAPON_TYPES = {"Ranged Weapons", "Melee Weapons", "C'tan Powers"}
RANGED_TYPES = {"Ranged Weapons", "C'tan Powers"}
ABILITY_TYPES = {"Abilities", "Triarch Abilities"}
GENERIC_WEAPONS = {"close combat weapon", "armoured bulk"}


def slug(name):
	"""Mirror Warhammer40k.Core/Text/Slugger.cs (drop apostrophes, non-alnum -> dash)."""
	out = []
	last_dash = False
	for ch in name:
		if ch.isalnum():
			out.append(ch.lower())
			last_dash = False
		elif ch in ("'", "\u2019"):
			continue
		elif out and not last_dash:
			out.append("-")
			last_dash = True
	return "".join(out).rstrip("-")


def chars(profile):
	return {c.get("name", ""): (c.get("$text", "") or "") for c in profile.get("characteristics", [])}


def collect(sel, profiles, rules, groups):
	"""Recursively gather profiles, rule names and wargear-group option selections."""
	for p in sel.get("profiles", []):
		profiles.append(p)
	for r in sel.get("rules", []):
		n = r.get("name")
		if n:
			rules.add(n)
	egid = sel.get("entryGroupId")
	grp = sel.get("group")
	if egid and grp:
		bucket = groups.setdefault(egid, {"group": grp, "options": []})
		bucket["options"].append(sel)
	for child in sel.get("selections", []):
		collect(child, profiles, rules, groups)


def subtree_profiles(sel):
	out = []

	def rec(s):
		for p in s.get("profiles", []):
			out.append(p)
		for c in s.get("selections", []):
			rec(c)

	rec(sel)
	return out


def weapon_from_profile(p):
	c = chars(p)
	typ = "Ranged" if p.get("typeName") in RANGED_TYPES else "Melee"
	raw_kw = (c.get("Keywords", "") or "").strip()
	keywords = [] if raw_kw in ("", "-") else [k.strip() for k in raw_kw.split(",") if k.strip() and k.strip() != "-"]
	return {
		"name": p.get("name", ""),
		"type": typ,
		"range": c.get("Range", ""),
		"attacks": c.get("A", ""),
		"skill": c.get("BS") or c.get("WS") or "",
		"strength": c.get("S", ""),
		"ap": c.get("AP", ""),
		"damage": c.get("D", ""),
		"keywords": keywords,
	}


def option_label(opt):
	"""Prefer the distinguishing weapon name; fall back to the selection name."""
	profs = subtree_profiles(opt)
	abilities = [p for p in profs if p.get("typeName") in ABILITY_TYPES]
	meaningful = []
	seen = set()
	for p in profs:
		if p.get("typeName") not in WEAPON_TYPES:
			continue
		nm = p.get("name", "")
		if nm.lower() in GENERIC_WEAPONS or nm in seen:
			continue
		seen.add(nm)
		meaningful.append(nm)
	if len(meaningful) == 1 and not abilities:
		return meaningful[0]
	return opt.get("name", "").strip()


def group_label(group):
	label = group.split("::")[-1].strip()
	# Model-choice groups are named after the unit size range ("10-20 Warriors"); relabel those.
	if re.match(r"^\s*\d", label) or label.lower() in ("unit composition",):
		return "Weapon"
	return label


def count_models(sel):
	total = 0
	if sel.get("type") == "model":
		total += max(0, sel.get("number", 0) or 0)
	for c in sel.get("selections", []):
		total += count_models(c)
	return total


def build(data, existing):
	existing_by_name = {d["name"]: d for d in existing.get("datasheets", [])}
	bindings = existing.get("pantheonBindings", [])

	roots = []
	for force in data["roster"].get("forces", []):
		roots.extend(force.get("selections", []))

	units = collections.OrderedDict()  # name -> accumulator

	for root in roots:
		profiles, rules, groups = [], set(), {}
		collect(root, profiles, rules, groups)
		unit_profiles = [p for p in profiles if p.get("typeName") == "Unit"]
		if not unit_profiles:
			continue  # configuration / non-datasheet entry

		name = root.get("name", "")
		acc = units.get(name)
		if acc is None:
			acc = {
				"name": name,
				"categories": root.get("categories", []),
				"stats": collections.OrderedDict(),
				"weapons": collections.OrderedDict(),
				"abilities": collections.OrderedDict(),
				"rules": set(),
				"groups": collections.OrderedDict(),
				"cost": None,
				"models": 0,
			}
			units[name] = acc

		for p in unit_profiles:
			key = p.get("name", "")
			if key not in acc["stats"]:
				c = chars(p)
				acc["stats"][key] = {
					"name": key, "m": c.get("M", ""), "t": c.get("T", ""), "sv": c.get("SV", ""),
					"w": c.get("W", ""), "ld": c.get("LD", ""), "oc": c.get("OC", ""),
				}

		for p in profiles:
			tn = p.get("typeName")
			if tn in WEAPON_TYPES:
				w = weapon_from_profile(p)
				k = (w["name"], w["type"], w["range"], w["attacks"], w["skill"], w["strength"], w["ap"], w["damage"])
				acc["weapons"].setdefault(k, w)
			elif tn in ABILITY_TYPES:
				nm = p.get("name", "")
				if nm and nm not in acc["abilities"]:
					c = chars(p)
					acc["abilities"][nm] = {"name": nm, "text": c.get("Description") or c.get("Effect") or ""}

		acc["rules"] |= rules

		for egid, g in groups.items():
			gg = acc["groups"].setdefault(egid, {"label": group_label(g["group"]), "options": collections.OrderedDict()})
			for opt in g["options"]:
				gg["options"].setdefault(option_label(opt), True)

		cost = next((c.get("value") for c in root.get("costs", []) if c.get("name") == "pts"), None)
		models = count_models(root)
		if cost is not None and acc["cost"] is None:
			acc["cost"] = cost
			acc["models"] = models

	sheets = []
	for name, acc in units.items():
		cat_names = []
		seen = set()
		primary_role = ""
		for c in acc["categories"]:
			cn = c.get("name", "")
			if cn and cn not in seen:
				seen.add(cn)
				cat_names.append(cn)
			if c.get("primary") and cn and cn != "Faction: Necrons" and not primary_role:
				primary_role = cn

		ex = existing_by_name.get(name)
		if ex and ex.get("pointsOptions"):
			points_options = ex["pointsOptions"]
		else:
			points_options = [{"models": acc["models"] or 1, "points": acc["cost"] if acc["cost"] is not None else 0}]
		smallest = min((po["points"] for po in points_options), default=0)

		ds_slug = slug(name)
		wargear_groups = []
		seen_option_sets = set()
		for egid, gg in acc["groups"].items():
			labels = []
			for label in gg["options"].keys():
				if label.lower() in GENERIC_WEAPONS:
					continue
				if label == name or slug(label) == ds_slug:
					continue  # unit/model name leaked in as an option
				if label not in labels:
					labels.append(label)
			# A real either/or wargear choice needs at least two distinct options;
			# single-option groups are mandatory default loadouts, not selectable.
			if len(labels) < 2:
				continue
			key = frozenset(labels)
			if key in seen_option_sets:
				continue  # duplicate group (same options under a different entryGroupId)
			seen_option_sets.add(key)
			options = []
			used = set()
			for label in labels:
				oid = slug(label) or "opt"
				base, i = oid, 1
				while oid in used:
					i += 1
					oid = f"{base}-{i}"
				used.add(oid)
				options.append({"id": oid, "name": label})
			wargear_groups.append({"id": egid, "name": gg["label"], "min": 0, "max": 1, "options": options})

		sheets.append({
			"name": name,
			"points": smallest,
			"primaryRole": primary_role,
			"isEpicHero": "Epic Hero" in cat_names,
			"isBattleline": "Battleline" in cat_names,
			"isDedicatedTransport": "Dedicated Transport" in cat_names,
			"isCharacter": "Character" in cat_names,
			"keywords": cat_names,
			"factionRules": sorted(acc["rules"]),
			"statProfiles": list(acc["stats"].values()),
			"abilities": list(acc["abilities"].values()),
			"weapons": list(acc["weapons"].values()),
			"pointsOptions": points_options,
			"wargearGroups": wargear_groups,
		})

	out = {"faction": existing.get("faction", "Necrons")}
	if existing.get("version") is not None:
		out["version"] = existing["version"]
	out["datasheets"] = sheets
	out["pantheonBindings"] = bindings
	return out


def main():
	ap = argparse.ArgumentParser(description=__doc__)
	ap.add_argument("--input", default="tools/necron-roster-export.json")
	ap.add_argument("--existing", default="Warhammer40k.Api/Seed/necron-catalogue-seed.json")
	ap.add_argument("--output", default="Warhammer40k.Api/Seed/necron-catalogue-seed.json")
	args = ap.parse_args()

	with open(args.input, "r", encoding="utf-8") as f:
		data = json.load(f)
	with open(args.existing, "r", encoding="utf-8") as f:
		existing = json.load(f)

	out = build(data, existing)

	with open(args.output, "w", encoding="utf-8") as f:
		json.dump(out, f, ensure_ascii=False, indent=2)
		f.write("\n")

	sheets = out["datasheets"]
	monsters = [s["name"] for s in sheets if "Monster" in s["keywords"]]
	print(f"datasheets: {len(sheets)}")
	print(f"monsters ({len(monsters)}): {monsters}")
	print(f"pantheon bindings: {len(out['pantheonBindings'])}")
	no_sizes = [s["name"] for s in sheets if not s["pointsOptions"] or s["pointsOptions"][0]["points"] == 0]
	if no_sizes:
		print(f"WARNING datasheets with 0-pt size: {no_sizes}")
	warriors = next((s for s in sheets if s["name"] == "Necron Warriors"), None)
	if warriors:
		print("Necron Warriors weapons:", [w["name"] for w in warriors["weapons"]])
		print("Necron Warriors wargear:", [(g["name"], [o["name"] for o in g["options"]]) for g in warriors["wargearGroups"]])


if __name__ == "__main__":
	main()
