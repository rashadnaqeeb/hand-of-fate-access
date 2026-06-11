"""Static trap-prefab recon: parses the game's scene files (UnityPy + typetrees
generated from the game's own Managed DLLs) and dumps each trap family's real
hierarchy - where the applicants, colliders, phases, and walk-in triggers
actually sit - then simulates the mod's ZoneSonification/ObjectBeacons scan
rules against it, so coverage can be verified from source instead of waiting
for a live run to fail. Re-run after a game update or when a new trap family
shows up in a level's "Trap_" inventory.

Usage: python tools/trap-recon.py [family-prefix ...]
Requires: pip install UnityPy TypeTreeGeneratorAPI
"""

import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME_DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Hand of Fate\Hand of Fate_Data"

# One scene per family is enough: a prefab's layout is identical everywhere.
SCENES = ["level128", "level27"]

COLLIDER_TYPES = {"BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider"}


def read_pptr(pptr):
    try:
        return pptr.read()
    except Exception:
        try:
            return pptr.deref().read()
        except Exception:
            return None


def unswap(value):
    """The generated typetree misreads only the m_Script PPtr: the real id comes
    out shifted left by 24 bits (verified: file id 16777216 >> 24 = 1, and known
    classes resolve at value >> 24)."""
    return value >> 24


class ScriptDb:
    """Resolves a MonoBehaviour's class name through its (byte-swapped) m_Script
    pointer: the externals table names the file holding the MonoScript."""

    def __init__(self):
        self.by_file = {}

    def _scripts_in(self, file_name):
        if file_name not in self.by_file:
            env = UnityPy.load(GAME_DATA + "\\" + file_name)
            table = {}
            for obj in env.objects:
                if obj.type.name == "MonoScript":
                    table[obj.path_id] = obj.read().m_ClassName
            self.by_file[file_name] = table
        return self.by_file[file_name]

    def class_name(self, assetsfile, script_pptr):
        try:
            file_id = unswap(script_pptr.m_FileID)
            path_id = unswap(script_pptr.m_PathID)
            if file_id <= 0 or file_id > len(assetsfile.externals):
                return "?"
            file_name = assetsfile.externals[file_id - 1].path.replace("\\", "/").split("/")[-1]
            return self._scripts_in(file_name).get(path_id, "?")
        except Exception:
            return "?"


SCRIPTS = ScriptDb()


def script_class(reader, mb):
    return SCRIPTS.class_name(reader.assets_file, mb.m_Script)


class Node:
    def __init__(self, name):
        self.name = name
        self.children = []
        self.colliders = []   # (type, isTrigger, enabled)
        self.behaviours = []  # (class, parsed)


def build_tree(transform, cache):
    go = read_pptr(transform.m_GameObject)
    node = Node(go.m_Name if go else "?")
    if go is not None:
        for entry in go.m_Component:
            pptr = entry[1] if isinstance(entry, tuple) else getattr(entry, "component", entry)
            try:
                reader = pptr.deref()
                type_name = reader.type.name
            except Exception:
                continue
            if type_name in COLLIDER_TYPES:
                col = reader.read()
                node.colliders.append((type_name, bool(col.m_IsTrigger), bool(col.m_Enabled)))
            elif type_name == "MonoBehaviour":
                try:
                    mb = reader.read()
                except Exception:
                    continue
                cls = script_class(reader, mb)
                node.behaviours.append((cls, mb))
                cache[reader.path_id] = (cls, mb, node)
    for child in transform.m_Children:
        child_t = read_pptr(child)
        if child_t is not None:
            node.children.append(build_tree(child_t, cache))
    return node


def owner_node_of(pptr, cache):
    try:
        pid = pptr.deref().path_id
    except Exception:
        return None
    hit = cache.get(pid)
    return hit[2] if hit else None


def collider_summary(node):
    return ", ".join(
        f"{t}({'trigger' if trig else 'solid'}{'' if en else ', disabled'})"
        for t, trig, en in node.colliders) or "none"


def describe(node, cache, indent=0):
    behaviours = []
    for cls, mb in node.behaviours:
        detail = ""
        if cls == "Trap":
            phases = [cache.get(p.deref().path_id, ("?",))[0] if safe_pid(p) else "?"
                      for p in mb.m_trapPhases]
            detail = f"(phases: {', '.join(phases)})"
        elif cls == "TrapPhaseTrigger":
            target = owner_node_of(mb.m_trigger, cache)
            detail = f"(trigger on '{target.name if target else '?'}', sets {bool(mb.m_enabled)})"
        elif cls == "TrapPhaseCollider":
            names = []
            for c in mb.m_colliders:
                try:
                    col = c.deref().read()
                    go = read_pptr(col.m_GameObject)
                    names.append(f"{go.m_Name if go else '?'}:{c.deref().type.name}"
                                 f"({'trigger' if col.m_IsTrigger else 'solid'})")
                except Exception:
                    names.append("?")
            detail = f"(sets {bool(mb.m_enabled)} on [{', '.join(names)}])"
        elif cls == "TrapPhaseCombatApplicant":
            target = owner_node_of(mb.m_combatApplicant, cache)
            detail = f"(applicant on '{target.name if target else '?'}', op={mb.m_operation})"
        elif cls in ("CombatApplicantTrigger", "CombatApplicantManual"):
            origins = []
            for o in getattr(mb, "m_origins", []) or []:
                try:
                    og = read_pptr(read_pptr(o).m_GameObject)
                    origins.append(og.m_Name if og else "?")
                except Exception:
                    origins.append("?")
            bits = [f"blockable={bool(mb.m_isBlockable)}"]
            if cls == "CombatApplicantTrigger":
                bits.append(f"applyToTargets={bool(mb.m_applyToTargets)}")
            if origins:
                bits.append("origins=[" + ", ".join(origins) + "]")
            detail = "(" + ", ".join(bits) + ")"
        elif cls == "TrapPhaseWaitForTime":
            detail = f"(time={getattr(mb, 'm_time', '?')})"
        behaviours.append(cls + detail)

    line = "  " * indent + node.name
    extras = []
    if behaviours:
        extras.append("[" + "; ".join(behaviours) + "]")
    if node.colliders:
        extras.append("colliders: " + collider_summary(node))
    if extras:
        line += "  " + "  ".join(extras)
    print(line)
    for child in node.children:
        describe(child, cache, indent + 1)


def safe_pid(pptr):
    try:
        pptr.deref()
        return True
    except Exception:
        return False


def find_components(node, cls, out):
    for c, mb in node.behaviours:
        if c == cls:
            out.append((node, mb))
    for child in node.children:
        find_components(child, cls, out)


def simulate_scan(root, cache):
    """Mirror ZoneSonification.ScanTraps + ObjectBeacons.FindWalkInTrigger."""
    traps = []
    find_components(root, "Trap", traps)
    for node, trap in traps:
        applicants = []
        find_components(node, "CombatApplicantTrigger", applicants)
        phase_classes = [cache.get(p.deref().path_id, ("?",))[0] if safe_pid(p) else "?"
                         for p in trap.m_trapPhases]
        manual = "TrapPhaseCombatApplicant" in phase_classes
        primed = "TrapPhaseWaitForTrigger" in phase_classes
        verdicts = []
        for anode, _ in applicants:
            if anode.colliders:
                verdicts.append(f"trigger '{anode.name}': own collider(s) -> VOICED")
            else:
                child_triggers = []
                collect_child_triggers(anode, child_triggers, top=True)
                if child_triggers:
                    verdicts.append(f"trigger '{anode.name}': child trigger collider(s) {child_triggers} -> VOICED")
                else:
                    phase_cols = phase_collider_count(node, cache)
                    if phase_cols:
                        verdicts.append(f"trigger '{anode.name}': {phase_cols} phase-referenced collider(s) -> VOICED")
                    else:
                        verdicts.append(f"trigger '{anode.name}': NO COLLIDER -> WARN, NOT VOICED")
        if manual:
            verdicts.append("manual emitter -> origin(s) VOICED" + (" as primed" if primed else " as arming"))
        if not applicants and not manual:
            verdicts.append("NO trigger or emitter -> WARN, NOT VOICED")
        print(f"  scan verdict ({node.name}, primed={primed}): " + "; ".join(verdicts))

    for cls in ("TrapExit", "TrapChest"):
        found = []
        find_components(root, cls, found)
        for node, _ in found:
            own = [c for c in node.colliders if c[1]]
            if own:
                print(f"  beacon verdict: {cls} '{node.name}' pings at its own trigger collider")
            else:
                childs = []
                collect_child_triggers(node, childs, top=True)
                if childs:
                    print(f"  beacon verdict: {cls} '{node.name}' pings at child trigger {childs[0]}")
                else:
                    print(f"  beacon verdict: {cls} '{node.name}' has NO trigger anywhere -> transform fallback")


def collect_child_triggers(node, out, top=False):
    if not top:
        # mirror the nested-component ownership skip
        nested = any(c in ("Trap", "CombatApplicantTrigger", "CombatApplicantManual",
                           "TrapChest", "TrapExit", "Loot") for c, _ in node.behaviours)
        if nested:
            return
        for t, trig, _ in node.colliders:
            if trig:
                out.append(f"{node.name}:{t}")
    for child in node.children:
        collect_child_triggers(child, out)


ROOT_CLASSES = ("Trap", "TrapExit", "TrapChest")


def transform_of(go):
    for entry in go.m_Component:
        pptr = entry[1] if isinstance(entry, tuple) else getattr(entry, "component", entry)
        try:
            reader = pptr.deref()
        except Exception:
            continue
        if reader.type.name == "Transform":
            return reader.read()
    return None


def main():
    name_filter = sys.argv[1:]
    gen = TypeTreeGenerator("5.3.7f1")
    gen.load_local_dll_folder(GAME_DATA + r"\Managed")

    seen = set()
    for scene in SCENES:
        env = UnityPy.load(GAME_DATA + "\\" + scene)
        env.typetree_generator = gen

        # The authoritative inventory: every object carrying a trap-system root
        # component, regardless of what the designer named it.
        roots = []
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                mb = obj.read()
            except Exception:
                continue
            cls = script_class(obj, mb)
            if cls not in ROOT_CLASSES:
                continue
            go = read_pptr(mb.m_GameObject)
            if go is None:
                continue
            key = f"{cls}:{go.m_Name}"
            if key in seen:
                continue
            if name_filter and not any(f in go.m_Name for f in name_filter):
                continue
            seen.add(key)
            roots.append((cls, go))

        for cls, go in roots:
            t = transform_of(go)
            if t is None:
                continue
            print(f"\n=== {cls} '{go.m_Name}' ({scene}) ===")
            cache = {}
            tree = build_tree(t, cache)
            describe(tree, cache)
            simulate_scan(tree, cache)


if __name__ == "__main__":
    main()
