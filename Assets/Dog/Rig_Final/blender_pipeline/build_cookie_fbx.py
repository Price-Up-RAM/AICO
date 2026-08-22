'''
쿠키 최종 빌드 v3.
v2 대비 추가:
  - 웨이트 오염 정리 (먼 본이 붙잡고 있는 웨이트만 보수적으로 제거) -> Sit_02 등 스파이크 완화
  - cookie_WalkCycle: Walk 의 실제 보행 구간만 잘라낸 무한루프용 클립
  - 텍스처를 FBX 내장이 아니라 .fbm 사이드카로 내보냄 (Unity 가 자동 인식)
'''
import bpy, sys, os, glob, statistics
from collections import defaultdict
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--")+1:]
target_fbx, latte_dir, out_fbx = argv[0], argv[1], argv[2]

FACE = ("eye", "nose", "mouth", "tongue")
SAMPLES = 24
WALK_LOOP = (27, 49)        # cookie_Walk 프레임 기준 실제 보행 1주기 (seam 0.04deg 측정값)
SMOOTH_ITERS = 4            # 웨이트 이웃평균 반복 횟수 (실측으로 고른 값)
SMOOTH_LAMBDA = 0.5

def imp(path):
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

def bind(obj, act):
    if obj.animation_data is None: obj.animation_data_create()
    obj.animation_data.action = act
    sl = list(getattr(act, "slots", []))
    if sl: obj.animation_data.action_slot = sl[0]

def hier(arm):
    out, seen = [], set()
    def walk(b):
        if b.name in seen: return
        seen.add(b.name); out.append(b.name)
        for c in b.children: walk(c)
    for b in arm.data.bones:
        if b.parent is None: walk(b)
    return out

bpy.ops.wm.read_factory_settings(use_empty=True)
tgt_objs = imp(target_fbx)
tgt = next(o for o in tgt_objs if o.type == 'ARMATURE')
mesh = max((o for o in tgt_objs if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
if tgt.animation_data: tgt.animation_data.action = None

# ---------- 1. 웨이트 정리 (스파이크 제거) ----------
# 스파이크의 원인은 "멀리 있는 본이 붙잡고 있는 웨이트" 가 아니라, 이웃 버텍스끼리
# 지배본이 급격히 바뀌는 불연속이다. 거리 기준으로 웨이트를 잘라내는 방식(prune)은
# 실측 결과 오히려 악화됨(Sit_02 17.1x -> 35.2x). 이웃 평균으로 부드럽게 만드는 쪽이 정답.
#   측정: none 17.1x -> eyefix+smooth4 4.5x, 10배 초과 엣지 15개 -> 0개
gname = {g.index: g.name for g in mesh.vertex_groups}
gobj = {g.name: g for g in mesh.vertex_groups}
nv = len(mesh.data.vertices)

W = [dict() for _ in range(nv)]
for v in mesh.data.vertices:
    for e in v.groups:
        n = gname.get(e.group)
        if n and e.weight > 0.0: W[v.index][n] = e.weight

# Eye_L / Eye_R 은 FBX 리프 아티팩트 본 -> 웨이트를 Head 로 이관 (RIGGING_NOTES 4-1 의 결정)
moved = 0
for d in W:
    for k in ("Eye_L", "Eye_R"):
        if k in d:
            d["Bip001 Head"] = d.get("Bip001 Head", 0.0) + d.pop(k)
            moved += 1
print("[weights] Eye_L/Eye_R -> Bip001 Head 이관: %d 항목" % moved)

# 이웃 평균 스무딩
adj = defaultdict(list)
for e in mesh.data.edges:
    a, b = e.vertices
    adj[a].append(b); adj[b].append(a)
for _ in range(SMOOTH_ITERS):
    new = []
    for i in range(nv):
        nb = adj[i]
        acc = defaultdict(float)
        for n, w in W[i].items(): acc[n] += (1.0 - SMOOTH_LAMBDA) * w
        if nb:
            f = SMOOTH_LAMBDA / len(nb)
            for j in nb:
                for n, w in W[j].items(): acc[n] += f * w
        s = sum(acc.values())
        new.append({n: w / s for n, w in acc.items() if w / s > 1e-4} if s > 1e-8 else dict(W[i]))
    W = new

for g in mesh.vertex_groups:
    g.remove(range(nv))
for i, d in enumerate(W):
    s = sum(d.values())
    if s <= 1e-8: continue
    for n, w in d.items():
        if w / s > 1e-4: gobj[n].add([i], w / s, 'REPLACE')
print("[weights] 스무딩 %d회 (lambda=%.2f) 적용" % (SMOOTH_ITERS, SMOOTH_LAMBDA))

# ---------- 2. 델타 리타겟 ----------
rest_t = {b.name: b.matrix_local.copy() for b in tgt.data.bones}
order = hier(tgt)
parent_of = {b.name: (b.parent.name if b.parent else None) for b in tgt.data.bones}
ROOT = order[0]
for pb in tgt.pose.bones: pb.rotation_mode = 'QUATERNION'
inv_world3 = tgt.matrix_world.to_3x3().inverted()
root_rest_inv3 = rest_t[ROOT].to_3x3().inverted()

def mesh_min_z():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    me = ev.to_mesh()
    m = ev.matrix_world
    z = min((m @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    return z

srcs = [s for s in sorted(glob.glob(os.path.join(latte_dir, "Pet_Dog_Latte_01@*.FBX")))
        if "_cookie" not in os.path.basename(s).lower()]
scn = bpy.context.scene
built = []

for spath in srcs:
    clip = os.path.basename(spath).split("@")[1].rsplit(".", 1)[0]
    s_objs = imp(spath)
    src = next(o for o in s_objs if o.type == 'ARMATURE')
    sact = src.animation_data.action if src.animation_data else None
    if sact is None:
        for o in s_objs: bpy.data.objects.remove(o, do_unlink=True)
        continue
    bind(src, sact)
    sname = sact.name
    rest_s = {b.name: b.matrix_local.copy() for b in src.data.bones}
    common = [n for n in order if n in src.data.bones and not any(k in n.lower() for k in FACE)]
    f0, f1 = [int(round(v)) for v in sact.frame_range]
    nf = f1 - f0 + 1

    goals = []
    for i in range(nf):
        scn.frame_set(f0 + i)
        ps = {pb.name: pb.matrix.copy() for pb in src.pose.bones}
        g = {}
        for n in order:
            if n in common:
                d = (ps[n].to_3x3() @ rest_s[n].to_3x3().inverted()).to_quaternion()
            else:
                d = Quaternion((1, 0, 0, 0))
            g[n] = Matrix.Translation(rest_t[n].translation) @ (d.to_matrix() @ rest_t[n].to_3x3()).to_4x4()
        goals.append(g)

    def write_pose(g, root_off_world):
        for n in order:
            p = parent_of[n]
            if p is None:
                basis = rest_t[n].inverted() @ g[n]
            else:
                basis = (rest_t[p].inverted() @ rest_t[n]).inverted() @ (g[p].inverted() @ g[n])
            pb = tgt.pose.bones[n]
            pb.rotation_quaternion = basis.to_quaternion()
            pb.scale = (1.0, 1.0, 1.0)
            if n == ROOT:
                pb.location = root_rest_inv3 @ (inv_world3 @ root_off_world)
            else:
                pb.location = Vector((0, 0, 0))

    def make_action(name, idxs):
        # 접지 오프셋: 해당 구간의 메쉬 최저 Z 중앙값을 0 으로
        step = max(1, len(idxs) // SAMPLES)
        zs = []
        for k in range(0, len(idxs), step):
            write_pose(goals[idxs[k]], Vector((0, 0, 0)))
            bpy.context.view_layer.update()
            zs.append(mesh_min_z())
        off = Vector((0.0, 0.0, -statistics.median(zs)))
        act = bpy.data.actions.new(name)
        bind(tgt, act)
        if not list(act.slots):
            tgt.animation_data.action_slot = act.slots.new(id_type='OBJECT', name=tgt.name)
        for f, i in enumerate(idxs, start=1):
            write_pose(goals[i], off)
            for n in order:
                pb = tgt.pose.bones[n]
                pb.keyframe_insert("rotation_quaternion", frame=f)
                if n == ROOT: pb.keyframe_insert("location", frame=f)
        built.append((act.name, len(idxs)))
        print("  %-26s frames=%4d  groundOffset %+.4f m" % (act.name, len(idxs), off.z))

    make_action("cookie_" + clip, list(range(nf)))
    if clip == "Walk":
        a, b = WALK_LOOP
        idxs = list(range(a - 1, b))           # cookie_Walk 프레임 a..b -> 인덱스 a-1..b-1
        make_action("cookie_WalkCycle", idxs)  # 첫프레임 == 마지막프레임 (seam 0.04deg)

    for o in s_objs: bpy.data.objects.remove(o, do_unlink=True)
    if sname in bpy.data.actions: bpy.data.actions.remove(bpy.data.actions[sname])

for a in list(bpy.data.actions):
    if not a.name.startswith("cookie_"):
        print("  drop stray:", a.name)
        bpy.data.actions.remove(a)

print("\nBUILT %d clips" % len(built))
scn.frame_start = 1
scn.frame_end = max(b[1] for b in built)
bpy.ops.object.select_all(action='DESELECT')
for o in tgt_objs:
    if o.name in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active = tgt
# 텍스처는 내장(embed)하지 않고 .fbm 사이드카로 복사 -> Unity 가 자동으로 잡는다
bpy.ops.export_scene.fbx(
    filepath=out_fbx, use_selection=True, apply_unit_scale=True,
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True, path_mode='COPY', embed_textures=False,
)
print("EXPORTED", out_fbx)
